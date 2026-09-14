import hashlib
import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from config import get_project_area

SECURITY_BLOCKED_PATTERNS = [
    re.compile(r"-----BEGIN (?:RSA |EC |DSA |OPENSSH )?PRIVATE KEY-----", re.IGNORECASE),
    re.compile(r"-----BEGIN CERTIFICATE-----", re.IGNORECASE),
    re.compile(r"password\s*[:=]\s*['\"][^'\"]{8,}['\"]", re.IGNORECASE),
]

MEMBER_DECLARATION_PATTERN = re.compile(
    r"^\s*(?:\[[^\]]+\]\s*)*(?:public|private|protected|internal|static|async|override|virtual|sealed|partial)\s+.*"
)


def contains_prohibited_secrets(content: str) -> bool:
    for pattern in SECURITY_BLOCKED_PATTERNS:
        if pattern.search(content):
            return True
    return False


def compute_sha256(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def chunk_file(file_path: Path, repo_root: Path) -> list[dict[str, Any]]:
    try:
        raw_text = file_path.read_text(encoding="utf-8", errors="replace")
    except Exception:
        return []

    if contains_prohibited_secrets(raw_text):
        return []

    rel_path = file_path.relative_to(repo_root).as_posix()
    project_area = get_project_area(rel_path)
    extension = file_path.suffix.lower() if file_path.suffix else file_path.name
    mtime = datetime.fromtimestamp(file_path.stat().st_mtime, tz=timezone.utc).isoformat()

    lines = raw_text.splitlines()
    if not lines:
        return []

    raw_chunks: list[tuple[int, int, str]] = []

    if extension == ".md":
        raw_chunks = _chunk_markdown(lines)
    elif extension == ".cs":
        raw_chunks = _chunk_csharp(lines)
    else:
        raw_chunks = _chunk_generic(lines)

    result = []
    for idx, (start_line, end_line, text) in enumerate(raw_chunks):
        cleaned_text = text.strip()
        if len(cleaned_text) < 25:
            continue

        prefix = f"// [{rel_path}:{start_line}-{end_line} | {project_area}]\n\n"
        full_content = prefix + cleaned_text

        chunk_dict = {
            "id": f"{rel_path}#L{start_line}-L{end_line}",
            "content": full_content,
            "relative_path": rel_path,
            "project_area": project_area,
            "extension": extension,
            "start_line": start_line,
            "end_line": end_line,
            "chunk_index": idx,
            "content_hash": compute_sha256(cleaned_text),
            "modified_time": mtime,
        }
        result.append(chunk_dict)

    return result


def _chunk_markdown(lines: list[str]) -> list[tuple[int, int, str]]:
    chunks: list[tuple[int, int, str]] = []
    current_lines: list[str] = []
    current_start = 1

    for idx, line in enumerate(lines, start=1):
        if line.startswith("#") and current_lines:
            text = "\n".join(current_lines).strip()
            if text:
                chunks.extend(_split_if_oversized(current_start, idx - 1, text, max_chars=1400))
            current_lines = []
            current_start = idx
        current_lines.append(line)

    if current_lines:
        text = "\n".join(current_lines).strip()
        if text:
            chunks.extend(_split_if_oversized(current_start, len(lines), text, max_chars=1400))

    return chunks


def _chunk_csharp(lines: list[str]) -> list[tuple[int, int, str]]:
    total = len(lines)
    if total <= 45:
        return [(1, total, "\n".join(lines))]

    boundaries = [1]
    for idx, line in enumerate(lines, start=1):
        stripped = line.strip()
        if stripped.startswith("//") or stripped.startswith("/*"):
            continue
        if MEMBER_DECLARATION_PATTERN.match(line):
            if idx - boundaries[-1] >= 12:
                boundaries.append(idx)

    if boundaries[-1] != total:
        boundaries.append(total + 1)

    chunks: list[tuple[int, int, str]] = []
    curr_start = boundaries[0]
    i = 1
    while i < len(boundaries):
        next_boundary = boundaries[i]
        span = next_boundary - curr_start
        if span >= 25 or i == len(boundaries) - 1:
            end_line = min(next_boundary - 1, total)
            if span > 65:
                sub_i = curr_start
                while sub_i <= end_line:
                    sub_end = min(sub_i + 40, end_line)
                    sub_text = "\n".join(lines[sub_i - 1 : sub_end])
                    if sub_text.strip():
                        chunks.append((sub_i, sub_end, sub_text))
                    if sub_end == end_line:
                        break
                    sub_i += 30
            else:
                chunk_text = "\n".join(lines[curr_start - 1 : end_line])
                if chunk_text.strip():
                    chunks.append((curr_start, end_line, chunk_text))
            curr_start = next_boundary
        i += 1

    return chunks


def _chunk_generic(lines: list[str]) -> list[tuple[int, int, str]]:
    total = len(lines)
    if total <= 50:
        return [(1, total, "\n".join(lines))]

    window = 40
    overlap = 10
    step = window - overlap

    chunks: list[tuple[int, int, str]] = []
    i = 0
    while i < total:
        start = i + 1
        end = min(i + window, total)
        chunk_text = "\n".join(lines[i:end])
        if chunk_text.strip():
            chunks.append((start, end, chunk_text))
        if end >= total:
            break
        i += step

    return chunks


def _split_if_oversized(
    start_line: int, end_line: int, text: str, max_chars: int = 1400
) -> list[tuple[int, int, str]]:
    if len(text) <= max_chars:
        return [(start_line, end_line, text)]

    paragraphs = text.split("\n\n")
    sub_chunks: list[tuple[int, int, str]] = []
    current_para_group: list[str] = []
    current_len = 0
    p_start = start_line

    for para in paragraphs:
        if current_len + len(para) > max_chars and current_para_group:
            block = "\n\n".join(current_para_group).strip()
            para_lines = block.count("\n") + 1
            sub_chunks.append((p_start, min(p_start + para_lines, end_line), block))
            p_start += para_lines
            current_para_group = [para]
            current_len = len(para)
        else:
            current_para_group.append(para)
            current_len += len(para)

    if current_para_group:
        block = "\n\n".join(current_para_group).strip()
        sub_chunks.append((p_start, end_line, block))

    return sub_chunks

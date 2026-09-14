import json
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import lancedb
from chunker import chunk_file, compute_sha256
from config import (
    BATCH_SIZE,
    DB_PATH,
    DEFAULT_MODEL_NAME,
    MANIFEST_PATH,
    REPO_ROOT,
    TABLE_NAME,
    escape_sql_literal,
    is_file_eligible,
)
from sentence_transformers import SentenceTransformer


class CodeIndexer:
    def __init__(self, model_name: str = DEFAULT_MODEL_NAME, db_path: Path = DB_PATH):
        self.model_name = model_name
        self.db_path = db_path
        self.repo_root = REPO_ROOT
        self.db_path.mkdir(parents=True, exist_ok=True)
        self.db = lancedb.connect(str(self.db_path))
        self._model: SentenceTransformer | None = None

    def _get_model(self) -> SentenceTransformer:
        if self._model is None:
            self._model = SentenceTransformer(self.model_name)
        return self._model

    def load_manifest(self) -> dict[str, dict[str, Any]]:
        if MANIFEST_PATH.exists():
            try:
                return json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
            except Exception:
                return {}
        return {}

    def save_manifest(self, manifest: dict[str, dict[str, Any]]) -> None:
        MANIFEST_PATH.parent.mkdir(parents=True, exist_ok=True)
        MANIFEST_PATH.write_text(
            json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8"
        )

    def scan_files(self) -> dict[str, Path]:
        eligible: dict[str, Path] = {}
        for p in self.repo_root.rglob("*"):
            if not p.is_file():
                continue
            rel = p.relative_to(self.repo_root).as_posix()
            if is_file_eligible(rel):
                eligible[rel] = p
        return eligible

    def run(self, force: bool = False) -> dict[str, Any]:
        start_time = time.time()
        existing_tables = set(self.db.table_names())

        if force:
            if TABLE_NAME in existing_tables:
                self.db.drop_table(TABLE_NAME)
            if MANIFEST_PATH.exists():
                MANIFEST_PATH.unlink()
            manifest = {}
        else:
            manifest = self.load_manifest()

        current_files = self.scan_files()

        new_files: list[str] = []
        modified_files: list[str] = []
        unchanged_files: list[str] = []
        deleted_files: list[str] = [p for p in manifest if p not in current_files]

        for rel_path, abs_path in current_files.items():
            try:
                content = abs_path.read_text(encoding="utf-8", errors="replace")
                f_hash = compute_sha256(content)
            except Exception:
                continue

            if rel_path not in manifest:
                new_files.append(rel_path)
            elif manifest[rel_path].get("content_hash") != f_hash:
                modified_files.append(rel_path)
            else:
                unchanged_files.append(rel_path)

        table_exists = TABLE_NAME in set(self.db.table_names())

        if not force and table_exists and not new_files and not modified_files and not deleted_files:
            tbl = self.db.open_table(TABLE_NAME)
            total_chunks = tbl.count_rows()
            elapsed = time.time() - start_time
            print("\n[INDEX] Database is up to date. No changes detected.")
            print(f"[INDEX] Scanned files: {len(current_files)} | Total indexed chunks: {total_chunks}")
            print(f"[INDEX] Elapsed time: {elapsed:.2f}s\n")
            return {
                "status": "up_to_date",
                "scanned_files": len(current_files),
                "unchanged_files": len(unchanged_files),
                "new_files": 0,
                "modified_files": 0,
                "deleted_files": 0,
                "chunks_indexed": 0,
                "total_chunks": total_chunks,
                "duration_seconds": elapsed,
            }

        files_to_index = new_files + modified_files
        all_new_chunks: list[dict[str, Any]] = []
        pending_manifest_updates: dict[str, dict[str, Any]] = {}

        for rel_path in files_to_index:
            abs_path = current_files[rel_path]
            chunks = chunk_file(abs_path, self.repo_root)
            all_new_chunks.extend(chunks)

            raw_text = abs_path.read_text(encoding="utf-8", errors="replace")
            pending_manifest_updates[rel_path] = {
                "content_hash": compute_sha256(raw_text),
                "modified_time": datetime.fromtimestamp(
                    abs_path.stat().st_mtime, tz=timezone.utc
                ).isoformat(),
                "chunk_count": len(chunks),
            }

        if all_new_chunks:
            model = self._get_model()
            texts_to_embed = [c["content"] for c in all_new_chunks]

            embeddings = []
            for i in range(0, len(texts_to_embed), BATCH_SIZE):
                batch = texts_to_embed[i : i + BATCH_SIZE]
                batch_emb = model.encode(
                    batch,
                    batch_size=BATCH_SIZE,
                    normalize_embeddings=True,
                    show_progress_bar=False,
                )
                embeddings.extend(batch_emb.tolist())

            for chunk, emb in zip(all_new_chunks, embeddings):
                chunk["vector"] = emb

        if not table_exists:
            if all_new_chunks:
                tbl = self.db.create_table(TABLE_NAME, all_new_chunks)
        else:
            tbl = self.db.open_table(TABLE_NAME)

            for d_path in deleted_files:
                escaped = escape_sql_literal(d_path)
                tbl.delete(f"relative_path = '{escaped}'")
                manifest.pop(d_path, None)

            for m_path in modified_files:
                escaped = escape_sql_literal(m_path)
                tbl.delete(f"relative_path = '{escaped}'")

            if all_new_chunks:
                tbl.add(all_new_chunks)

        manifest.update(pending_manifest_updates)
        self.save_manifest(manifest)

        final_tbl = self.db.open_table(TABLE_NAME)
        total_chunks = final_tbl.count_rows()
        elapsed = time.time() - start_time
        model_dim = (
            384 if self._model is None else self._model.get_sentence_embedding_dimension()
        )

        print("\n=======================================================")
        print("          FLEETOPS CODE SEARCH INDEX REPORT            ")
        print("=======================================================")
        print(f" Model Used:        {self.model_name} (dim: {model_dim})")
        print(f" Database Path:     {self.db_path}")
        print(f" Files Scanned:     {len(current_files)}")
        print(f"   - Unchanged:     {len(unchanged_files)}")
        print(f"   - New:           {len(new_files)}")
        print(f"   - Modified:      {len(modified_files)}")
        print(f"   - Deleted:       {len(deleted_files)}")
        print(f" Chunks Indexed:    {len(all_new_chunks)}")
        print(f" Total DB Chunks:   {total_chunks}")
        print(f" Indexing Duration: {elapsed:.2f}s")
        print("=======================================================\n")

        return {
            "status": "indexed",
            "model": self.model_name,
            "dimension": model_dim,
            "scanned_files": len(current_files),
            "unchanged_files": len(unchanged_files),
            "new_files": len(new_files),
            "modified_files": len(modified_files),
            "deleted_files": len(deleted_files),
            "chunks_indexed": len(all_new_chunks),
            "total_chunks": total_chunks,
            "duration_seconds": elapsed,
        }

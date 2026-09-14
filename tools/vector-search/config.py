from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
DB_PATH = REPO_ROOT / ".vector-db"
MANIFEST_PATH = DB_PATH / "manifest.json"
TABLE_NAME = "fleetops_code"
DEFAULT_MODEL_NAME = "all-MiniLM-L6-v2"
BATCH_SIZE = 32

IGNORED_DIRS = {
    "bin",
    "obj",
    ".vs",
    ".vscode",
    ".idea",
    ".git",
    "testresults",
    "coverage",
    "node_modules",
    "dist",
    ".vector-db",
    "tools",
    "scratch",
    "__pycache__",
    ".pytest_cache",
    ".venv",
    "venv",
}

IGNORED_FILE_PATTERNS = [
    ".env",
    ".env.*",
    "*.key",
    "*.pem",
    "*.pfx",
    "*.p12",
    "*.cert",
    "*.dll",
    "*.exe",
    "*.pdb",
    "*.trx",
    "*.coverage*",
    "*.user",
    "*.suo",
    "*.userprefs",
    "*.DS_Store",
    "thumbs.db",
]

ALLOWED_EXTENSIONS = {
    ".cs",
    ".csproj",
    ".sln",
    ".json",
    ".yaml",
    ".yml",
    ".md",
    ".props",
    ".js",
    ".ps1",
    ".sh",
}

SPECIAL_ALLOWED_FILES = {
    "dockerfile",
    ".dockerignore",
    ".editorconfig",
    ".gitattributes",
}


def is_file_eligible(rel_path_str: str) -> bool:
    normalized = rel_path_str.replace("\\", "/").strip().lower()
    parts = normalized.split("/")

    for part in parts[:-1]:
        if part in IGNORED_DIRS:
            return False

    filename = parts[-1]

    if filename.startswith(".env"):
        return False

    for pat in IGNORED_FILE_PATTERNS:
        if pat.startswith("*") and filename.endswith(pat[1:]):
            return False
        if pat == filename:
            return False

    suffix = Path(filename).suffix.lower()
    if suffix in ALLOWED_EXTENSIONS or filename in SPECIAL_ALLOWED_FILES:
        return True

    return False


def get_project_area(rel_path_str: str) -> str:
    path_lower = rel_path_str.replace("\\", "/").strip().lower()
    if path_lower.startswith("src/fleetops.domain"):
        return "Domain"
    if path_lower.startswith("src/fleetops.application"):
        return "Application"
    if path_lower.startswith("src/fleetops.infrastructure"):
        return "Infrastructure"
    if path_lower.startswith("src/fleetops.api"):
        return "API"
    if path_lower.startswith("tests/"):
        return "Tests"
    if path_lower.startswith("docs/"):
        return "Documentation"
    if path_lower.startswith("load-tests/"):
        return "LoadTests"
    if path_lower.startswith("keycloak/"):
        return "Keycloak"
    if path_lower.startswith(".github/"):
        return "CI"
    if path_lower.endswith(".md"):
        return "Documentation"
    return "Configuration"


def escape_sql_literal(val: str) -> str:
    """Safely escape string literal for LanceDB SQL filter expressions."""
    return val.replace("\\", "/").replace("'", "''")

import argparse
import sys
from pathlib import Path

current_dir = Path(__file__).resolve().parent
if str(current_dir) not in sys.path:
    sys.path.insert(0, str(current_dir))

from config import DEFAULT_MODEL_NAME
from indexer import CodeIndexer


def main():
    parser = argparse.ArgumentParser(
        description="FleetOps Local Semantic Code Search — Indexer"
    )
    parser.add_argument(
        "--force",
        "-f",
        action="store_true",
        help="Rebuild the entire vector index from scratch.",
    )
    parser.add_argument(
        "--model",
        "-m",
        default=DEFAULT_MODEL_NAME,
        help=f"SentenceTransformer embedding model name (default: {DEFAULT_MODEL_NAME}).",
    )

    args = parser.parse_args()

    indexer = CodeIndexer(model_name=args.model)
    indexer.run(force=args.force)


if __name__ == "__main__":
    main()

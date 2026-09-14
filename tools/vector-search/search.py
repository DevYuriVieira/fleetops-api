import argparse
import sys
from pathlib import Path

current_dir = Path(__file__).resolve().parent
if str(current_dir) not in sys.path:
    sys.path.insert(0, str(current_dir))

from config import DEFAULT_MODEL_NAME
from searcher import CodeSearcher


def main():
    parser = argparse.ArgumentParser(
        description="FleetOps Local Semantic Code Search — Search CLI"
    )
    parser.add_argument("query", help="Natural language or code semantic search query.")
    parser.add_argument(
        "--top-k",
        "-k",
        type=int,
        default=5,
        help="Number of top search results to return (default: 5).",
    )
    parser.add_argument(
        "--area",
        "-a",
        default=None,
        help="Filter results by project area (e.g., Domain, Application, Infrastructure, API, Tests, Documentation).",
    )
    parser.add_argument(
        "--ext",
        "-e",
        default=None,
        help="Filter results by file extension (e.g., .cs, .md, .json).",
    )
    parser.add_argument(
        "--model",
        "-m",
        default=DEFAULT_MODEL_NAME,
        help=f"Embedding model name used (default: {DEFAULT_MODEL_NAME}).",
    )

    args = parser.parse_args()

    searcher = CodeSearcher(model_name=args.model)
    try:
        results = searcher.search(
            query=args.query,
            top_k=args.top_k,
            area=args.area,
            extension=args.ext,
        )
        searcher.print_results(args.query, results)
    except FileNotFoundError as ex:
        print(f"\n[ERROR] {ex}\n", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()

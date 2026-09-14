from pathlib import Path
from typing import Any

import lancedb
from config import DB_PATH, DEFAULT_MODEL_NAME, TABLE_NAME, escape_sql_literal
from sentence_transformers import SentenceTransformer


class CodeSearcher:
    def __init__(self, model_name: str = DEFAULT_MODEL_NAME, db_path: Path = DB_PATH):
        self.model_name = model_name
        self.db_path = db_path
        self._model: SentenceTransformer | None = None
        self._table = None

    def _get_model(self) -> SentenceTransformer:
        if self._model is None:
            self._model = SentenceTransformer(self.model_name)
        return self._model

    def _get_table(self):
        if self._table is not None:
            return self._table

        if not self.db_path.exists():
            raise FileNotFoundError(
                f"Database not found at '{self.db_path}'. Please run 'python tools/vector-search/index.py' first."
            )

        db = lancedb.connect(str(self.db_path))
        if TABLE_NAME not in set(db.table_names()):
            raise FileNotFoundError(
                f"Table '{TABLE_NAME}' not found in '{self.db_path}'. Please run 'python tools/vector-search/index.py' first."
            )

        self._table = db.open_table(TABLE_NAME)
        return self._table

    def search(
        self,
        query: str,
        top_k: int = 5,
        area: str | None = None,
        extension: str | None = None,
    ) -> list[dict[str, Any]]:
        table = self._get_table()
        model = self._get_model()

        query_vector = model.encode([query], normalize_embeddings=True)[0].tolist()

        search_query = table.search(query_vector).metric("cosine")

        where_clauses = []
        if area:
            where_clauses.append(f"project_area = '{escape_sql_literal(area)}'")
        if extension:
            ext_val = extension if extension.startswith(".") else f".{extension}"
            where_clauses.append(f"extension = '{escape_sql_literal(ext_val)}'")

        if where_clauses:
            search_query = search_query.where(" AND ".join(where_clauses))

        raw_results = search_query.limit(top_k).to_list()

        results = []
        for r in raw_results:
            distance = float(r.get("_distance", 0.0))
            score = max(0.0, 1.0 - distance)

            content = r.get("content", "")
            lines = content.splitlines()
            body_lines = lines[2:] if len(lines) > 2 and lines[0].startswith("// [") else lines
            snippet = "\n".join(body_lines[:15]).strip()
            if len(body_lines) > 15:
                snippet += "\n    ..."

            results.append(
                {
                    "score": score,
                    "distance": distance,
                    "relative_path": r.get("relative_path"),
                    "start_line": r.get("start_line"),
                    "end_line": r.get("end_line"),
                    "project_area": r.get("project_area"),
                    "extension": r.get("extension"),
                    "id": r.get("id"),
                    "snippet": snippet,
                    "full_content": content,
                }
            )

        return results

    def print_results(self, query: str, results: list[dict[str, Any]]) -> None:
        print("\n" + "=" * 70)
        print(f" Query: \"{query}\"")
        print(f" Results found: {len(results)}")
        print("=" * 70 + "\n")

        if not results:
            print("No matching results found.\n")
            return

        for idx, item in enumerate(results, start=1):
            score_str = f"{item['score']:.4f} (distance: {item['distance']:.4f})"
            path_str = item["relative_path"]
            lines_str = f"lines {item['start_line']}-{item['end_line']}"
            area_str = f"area: {item['project_area']}"

            print(f"[{idx}] score: {score_str}")
            print(f"    {path_str}")
            print(f"    {lines_str} | {area_str}")
            print()

            indented_snippet = "\n".join(f"    {line}" for line in item["snippet"].splitlines())
            print(indented_snippet)
            print("\n" + "-" * 70 + "\n")

# FleetOps — Local Semantic Code Search

Ferramenta local de busca semântica sobre o código-fonte, arquitetura e documentação do projeto **FleetOps API**.

A ferramenta indexa o repositório em um banco vetorial local persistente (**LanceDB**) utilizando embeddings neurais locais (**SentenceTransformers**). Toda a execução ocorre 100% offline no ambiente local do desenvolvedor, sem dependência de serviços externos ou APIs pagas.

---

## 1. Arquitetura da Ferramenta

```
FleetOps Repository (src/, tests/, docs/, config)
                     │
                     ▼
             [ Chunker Estrutural ]
  (C# methods/classes, Markdown sections, Configs)
                     │
                     ▼
         [ SentenceTransformers ]
           (all-MiniLM-L6-v2)
                     │
                     ▼
         [ LanceDB: .vector-db/ ]
        (Tabela: fleetops_code)
                     │
                     ▼
             [ Search CLI ]
  (Busca vetorial por similaridade de cosseno)
```

- **Isolamento de Runtime:** A ferramenta reside exclusivamente no diretório `tools/vector-search/`. Nenhuma dependência Python ou LanceDB é introduzida no runtime .NET 10 ou na Clean Architecture da API.
- **Banco Vetorial Local:** Persistido no diretório raiz `.vector-db/`. Este diretório é um artefato derivado local e está configurado no `.gitignore` (não deve ser commitado).

---

## 2. Requisitos e Instalação

### Pré-requisitos
- Python 3.10+ (testado no Python 3.13)
- `pip`

### Instalação de Dependências

No terminal, a partir da raiz do repositório:

```bash
pip install -r tools/vector-search/requirements.txt
```

Dependências principais:
- `lancedb`: Banco de dados vetorial embutido e serverless de alta performance.
- `sentence-transformers`: Framework para computação de embeddings locais via Hugging Face.
- `pyarrow`: Manipulação de dados colunares e interoperabilidade de schemas.

---

## 3. Modelo de Embeddings

- **Modelo Padrão:** `all-MiniLM-L6-v2` (Sentence-Transformers)
- **Dimensão dos Vetores:** 384 dimensões
- **Tamanho:** ~80 MB
- **Execução:** 100% CPU local (com suporte automático a GPU/CUDA se disponível)

> **Nota sobre o primeiro download:** Na primeira execução da ferramenta, os pesos do modelo (`all-MiniLM-L6-v2`) serão baixados automaticamente pelo Hugging Face para o cache local do usuário (`~/.cache/huggingface/hub`). A partir de então, o modelo opera totalmente offline sem requisições de rede.

---

## 4. Indexação

### Primeira Indexação (Indexação Completa)

Para escanear e indexar todos os arquivos relevantes do projeto:

```bash
python tools/vector-search/index.py
```

Saída esperada:
```text
=======================================================
          FLEETOPS CODE SEARCH INDEX REPORT            
=======================================================
 Model Used:        all-MiniLM-L6-v2 (dim: 384)
 Database Path:     c:\...\fleetops\.vector-db
 Files Scanned:     254
   - Unchanged:     0
   - New:           254
   - Modified:      0
   - Deleted:       0
 Chunks Indexed:    1200+
 Total DB Chunks:   1200+
 Indexing Duration: ~15s
=======================================================
```

### Indexação Incremental (Idempotente)

Execuções subsequentes utilizam o manifesto local (`.vector-db/manifest.json`) baseado em hash SHA-256 e timestamp de modificação:

```bash
python tools/vector-search/index.py
```

Comportamento:
- **Arquivos inalterados:** Nenhum embedding novo é gerado. A verificação é quase instantânea.
- **Arquivos modificados:** Apenas os chunks antigos do arquivo modificado são removidos do banco e reindexados.
- **Arquivos removidos:** Os chunks pertencentes aos arquivos deletados são expurgados do LanceDB.
- **Idempotência:** Executar repetidamente sem alterações não duplica registros.

### Reindexação Forçada (Full Rebuild)

Para forçar a reconstrução completa do índice a partir do zero:

```bash
python tools/vector-search/index.py --force
```

---

## 5. Busca Semântica

A CLI de busca permite consultas em linguagem natural e termos de código.

### Uso Básico

```bash
python tools/vector-search/search.py "onde é implementado o Transactional Outbox?"
```

Exemplo de saída no terminal:
```text
======================================================================
 Query: "onde é implementado o Transactional Outbox?"
 Results found: 5
======================================================================

[1] score: 0.7612 (distance: 0.2388)
    src/FleetOps.Infrastructure/Services/OutboxService.cs
    lines 39-79 | area: Infrastructure

    public async Task<int> ProcessPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            List<OutboxMessage> messages;
            if (_context.Database.IsNpgsql())
            {
                messages = await _context.OutboxMessages
                    .FromSqlRaw(
                        """
                        SELECT * FROM outbox_messages
                        WHERE processed_on_utc IS NULL AND attempts < {0}
                        ORDER BY occurred_on_utc
                        LIMIT {1}
                        FOR UPDATE SKIP LOCKED
                        """, ...
    ...
```

### Opções Avançadas da Busca

| Flag | Descrição | Exemplo |
|---|---|---|
| `--top-k`, `-k` | Quantidade de resultados a retornar (padrão: 5) | `-k 10` |
| `--area`, `-a` | Filtrar por área do projeto (`Domain`, `Application`, `Infrastructure`, `API`, `Tests`, `Documentation`) | `-a Infrastructure` |
| `--ext`, `-e` | Filtrar por extensão de arquivo (`.cs`, `.md`, `.json`, `.yaml`) | `-e .cs` |
| `--model`, `-m` | Especificar outro modelo SentenceTransformer | `-m BAAI/bge-small-en-v1.5` |

Exemplos:

```bash
# Buscar testes relacionados a falhas de RabbitMQ
python tools/vector-search/search.py "RabbitMQ connection failure" --area Tests --top-k 5

# Buscar decisões de arquitetura sobre autenticação OIDC
python tools/vector-search/search.py "Keycloak OIDC Resource Server" --area Documentation

# Buscar validação de JWT apenas em arquivos C#
python tools/vector-search/search.py "ValidateIssuerSigningKey ClockSkew" --ext .cs
```

---

## 6. Alteração de Modelo

Caso deseje utilizar outro modelo SentenceTransformer compatível (ex: `BAAI/bge-small-en-v1.5` ou `sentence-transformers/all-mpnet-base-v2`):

```bash
python tools/vector-search/index.py --model BAAI/bge-small-en-v1.5 --force
python tools/vector-search/search.py "Transactional Outbox" --model BAAI/bge-small-en-v1.5
```

---

## 7. Segurança e Higiene de Repositório

- **Prevenção de Inclusão de Credenciais:** O indexador ignora automaticamente arquivos `.env*`, chaves privadas (`*.key`, `*.pem`, `*.pfx`), certificados e padrões contendo material criptográfico sensível.
- **Diretórios Ignorados:** `bin/`, `obj/`, `.vs/`, `.vscode/`, `.idea/`, `.git/`, `TestResults/`, `coverage/`, `node_modules/`, `dist/`, `.vector-db/` e `tools/`.
- **Banco Vetorial Local:** A pasta `.vector-db/` está listada no `.gitignore` para assegurar que nenhum artefato binário ou derivado seja commitado no controle de versão.

---

## 8. Limitações Conhecidas

- **Context Window:** O modelo `all-MiniLM-L6-v2` aceita até 256/512 tokens por chunk. Chunks maiores são subdivididos estruturalmente para garantir que o embedding represente o bloco sem perda por truncamento.
- **Ambiente Offline Restrito:** O download inicial do modelo requer conexão com a internet. Após a primeira carga, todo o processamento é estritamente offline.

# Visor.CLI

**Visor.CLI** is a command-line interface tool designed for **Database-First** scaffolding. It connects to your database (SQL Server or PostgreSQL), scans for Stored Procedures and Table Types, and automatically generates strictly-typed C# interfaces and DTOs for the Visor ORM.

## 🚀 Installation

You can install the tool globally (once published) or run it locally from the source.

### Run from Source
```bash
dotnet run --project src/Visor.CLI/Visor.CLI.csproj -- run --help
```

### Global Installation (Local Source)
To build and install the tool globally from your local source:

1.  **Pack**:
    ```bash
    dotnet pack src/Visor.CLI/Visor.CLI.csproj -c Release --output nupkgs
    ```

2.  **Install**:
    ```bash
    dotnet tool install --global --add-source ./nupkgs Visor.CLI
    ```

### Global Installation (NuGet)
Once published to NuGet.org:
```bash
dotnet tool install --global Visor.CLI
```

---

## 🎮 Usage Modes

Visor.CLI supports two modes: **Interactive** (for developers) and **Headless** (for CI/CD).

### 1. Interactive Mode (Recommended)
Simply run the command without arguments (or with partial arguments). The tool will prompt you for missing details.

```bash
visor run
```

**Workflow:**
1.  **Select Provider:** Choose between `mssql` or `postgres`.
2.  **Connection String:** Enter your database connection string.
3.  **Select Procedures:** A multi-selection menu will appear. Use `Space` to select procedures and `Enter` to confirm.
4.  **Save Config:** Optionally save your settings to `visor.json` for future runs.

### 2. Headless Mode (CI/CD)
Provide all required arguments to skip prompts. This is useful for automated build pipelines.

**SQL Server Example:**
```bash
visor run \
  --provider mssql \
  --connection "Server=.;Database=MyDb;Integrated Security=True;TrustServerCertificate=True;" \
  --output ./src/MyProject/Data \
  --namespace MyProject.Data
```

**PostgreSQL Example:**
```bash
visor run \
  -p postgres \
  -c "Host=localhost;Database=mydb;Username=postgres;Password=password" \
  -o ./Generated \
  -n Visor.Generated
```

---

## ⚙️ Configuration (visor.json)

To avoid typing connection strings repeatedly, Visor.CLI supports a persistent configuration file.
When running interactively, the tool will offer to create a `visor.json` file in the current directory.

**Example `visor.json`:**
```json
{
  "provider": "mssql",
  "connectionString": "Server=.;Database=MyDb;Integrated Security=True;TrustServerCertificate=True;",
  "output": "./src/MyDomain/Data",
  "namespace": "MyDomain.Data"
}
```

> **Security Note:** If your connection string contains secrets, ensure `visor.json` is added to your `.gitignore`.

---

## 📝 Command Options

| Option | Alias | Description | Default |
| :--- | :--- | :--- | :--- |
| `--provider` | `-p` | Database provider (`mssql` or `postgres`). | - |
| `--connection` | `-c` | The database connection string. | - |
| `--output` | `-o` | Output directory for generated files. | `./Generated` |
| `--namespace` | `-n` | The C# Namespace for the generated code. | `Visor.Generated` |

---

## ⚡ Features

*   **Strict Mapping:** Automatically maps SQL types to C# types (e.g., `int` -> `int`, `varchar` -> `string`).
*   **Table-Valued Parameters (TVP):** Generates DTO classes for User-Defined Table Types.
*   **Postgres Arrays:** Automatically maps PostgreSQL arrays (e.g., `integer[]`) to `List<int>`.
*   **Sanitization:** Automatically escapes C# keywords (e.g., a column named `class` becomes `@class`).

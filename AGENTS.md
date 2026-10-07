# AGENTS.md — Sabores Wanka (POS Desktop)

> Archivo OPERATIVO: rutas, comandos, arquitectura y reglas de negocio.
> El PROCESO de trabajo (ciclo de 7 pasos, protocolo senior, gate, spec-first, ADRs, mojibake)
> vive en **PROTOCOLO.md** (mismo directorio). Léelo ANTES de la primera edición de cada sesión.

---

## PROJECT: Sabores Wanka (POS C# Desktop)

### Project

WinUI 3 (Windows App SDK 2.5.1) desktop POS. .NET 8 (`net8.0-windows10.0.19041.0`), `UseWinUI=true`, `Nullable=enable`. Un solo `.sln` con la app principal. Namespace raíz: `sabores_wanka`. Por defecto empaquetado (MSIX); `launchSettings.json` expone también el perfil *Unpackaged*.

Root folder (repo): `C:\Users\Erick\Desktop\sabores wanka\`
Código real (capa anidada): `C:\Users\Erick\Desktop\sabores wanka\sabores wanka\`
Obsidian vault: `C:\Users\Erick\Desktop\sabores wanka\Historial_Sabores_Wanka\`
Context bridge: `C:\Users\Erick\Desktop\sabores wanka\sabores wanka\CONTEXTO.md` (reglas de negocio en cache — LEER ANTES de codificar)

> **REGLA DE RUTAS (obligatoria):** cuando entregue al usuario un archivo que deba
> abrir, ejecutar o revisar MANUALMENTE (.sql, .bat, .ps1, configs), dar SIEMPRE la
> **ruta absoluta exacta** en un bloque de código aparte. NO usar rutas relativas.
> El proyecto real vive en una carpeta ANIDADA: `C:\Users\Erick\Desktop\sabores wanka\sabores wanka\`
> (no confundir con `C:\Users\Erick\Desktop\sabores wanka\` que es la raíz del repo).
> Todo el código (`.cs`, `.xaml`, `.csproj`, `.sln`, `Views/`) está en la capa anidada.

### Build & Dev Commands

```powershell
# Build (Debug, win-x64)
dotnet build "sabores wanka\sabores wanka.csproj" -c Debug -r win-x64 -p:Platform=x64

# Build sin tocar el exe en uso (la app corriendo bloquea el binario):
dotnet build "sabores wanka\sabores wanka.csproj" -c Debug -r win-x64 -p:Platform=x64 -o <carpeta-temp>

# Ejecutar (unpackaged)
dotnet run --project "sabores wanka\sabores wanka.csproj" -c Debug
```

`Platforms`: `x86;x64;ARM64`. No hay proyecto de tests todavía (ver §Testing).

### Architecture

- **`App.xaml` / `App.xaml.cs`**: bootstrap de la app. `OnLaunched` crea y activa `MainWindow`. Es el equivalente a `main()`.
- **`MainWindow.xaml` / `MainWindow.xaml.cs`**: shell de la ventana. Cablea la barra lateral (`MainSidebar`, tipo `Views.Components.BarraLateral`) y un contenedor de contenido (`MainContent`). Los eventos `PuntoDeVentaSelected` / `HistorialSelected` intercambian `MainContent.Content`.
- **`Views/Components/BarraLateral.xaml`**: barra de navegación lateral; expone eventos de selección de sección.
- **`Views/punto de venta/`**: `PuntoDeVentaView` (namespace `Views.PuntoDeVenta`), pantalla principal de venta y `2.0_mesas` (gestión de mesas).
- **`Views/historial de ventas/`**: `HistorialDeVentasView` (namespace `Views.HistorialDeVentas`), dashboard/historial.
- **`Assets/`**: logos y splash del paquete MSIX.
- **Maquetas de referencia (NO compilan)**: `code.html`, `screen.png` y `stitch_*.zip` dentro de cada carpeta de `Views/` son diseños exportados de Stitch. Son la referencia visual para replicar la UI en XAML.

### Convenciones de UI (WinUI 3)

- Namespaces por carpeta: `Views.PuntoDeVenta`, `Views.HistorialDeVentas`, `Views.Components`.
- La navegación se resuelve sustituyendo `MainContent.Content` (sin Frame/NavigationService todavía).
- Al replicar diseños de Stitch, priorizar `Grid` + `VisualStateManager` sobre layouts absolutos; respetar `x:Name` en PascalCase y eventos `XxxSelected`.

### Encoding & Editing Rules

- **Todos los archivos de código y notas DEBEN ser UTF-8 con BOM** (`charset = utf-8-bom` en `.editorconfig`). Herramientas que quitan el BOM o escriben ANSI/Windows-1252 corrompen los caracteres españoles (ñ, tildes, acentos).
- **Excepción:** `.json` / `.jsonc` van **sin BOM** para no romper parsers.
- Line endings: CRLF. Indent: 4 espacios.
- Si ves caracteres corruptos, el encoding se rompió.
- Al editar con herramientas que puedan normalizar EOL, verificar DESPUÉS: BOM (primeros 3 bytes `EF BB BF`) y cero líneas `LF-only` (byte 0x0A sin 0x0D previo).

### 📝 Directiva de comentarios en código (PROMPT BASE — obligatoria)

Al añadir o modificar código, usar SIEMPRE comentarios orientados a IA:

- **Qué explicar:** intención del diseño, responsabilidades, entradas/salidas, reglas importantes, dependencias y qué NO debe modificarse.
- **Formato:** lenguaje simple, preciso y consistente. **Máx 2–4 líneas por método o bloque.**
- **Propósito:** claridad para futuras extensiones y cambios seguros.
- **AL EDITAR un método con comentarios existentes: BORRAR los comentarios antiguos y actualizarlos** (los comentarios viejos/duplicados acumulan mojibake y desinformación de IAs anteriores).
- **NO** comentar lo obvio (nombres autoexplicativos); comentar el POR QUÉ y las trampas.
- Verificar al terminar: el comentario nuevo NO contiene caracteres corruptos (ñ/acentos ok, nunca `Ã¡`/`â€™`/U+FFFD).

### Testing

```powershell
# Aún NO existe proyecto de tests. Cuando se cree, será xUnit + Moq
# (espejo de CollabCart.Tests del proyecto hermano) en un .csproj aparte.
dotnet test <Proyecto>.Tests\<Proyecto>.Tests.csproj
```

Regla: ninguna feature se da por cerrada sin un test que la cubra. Cuando se cree el proyecto de tests, añadir un guard de arquitectura (que las capas de dominio no referencien infraestructura).

### Other Config

- `.editorconfig` — charset UTF-8 BOM (código/notas) + CRLF + 4 espacios (JSON sin BOM).
- `opencode.json` — configuración de opencode + servidor MCP `codebase-memory`.
- `Properties/launchSettings.json` — perfiles *Package* / *Unpackaged*.
- `Package.appxmanifest` — manifiesto MSIX (solo build empaquetado).
- `app.manifest` — manifiesto Win32 (DPI, etc.).

---

## 🧠 SISTEMA DE MEMORIA ANTI-ALUCINACIONES (Graph RAG v2)

TIENES PROHIBIDO entrar en bucles de error. Para evitarlo, usamos **UNA** bóveda de Obsidian como memoria a largo plazo de errores y correcciones **de este proyecto**.

Esta memoria NO se recupera por adivinanza de nombres. Se recupera de forma **determinista** vía `INDEX.md` (mapa `ruta de código → nota`) y se navega vía **backlinks** (`grep "[[archivo]]"`) para análisis de impacto automático.

**REGLA DE FRESCURA:** el vault solo sabe lo que se le ESCRIBE. Todo fix (tuyo o de la IA) debe documentarse el MISMO día en la nota del archivo tocado + INDEX.

### Bóveda de este proyecto

| Ámbito | Vault | Índice maestro |
|---|---|---|
| `sabores wanka\` (POS C#) | `Historial_Sabores_Wanka\` | `Historial_Sabores_Wanka\INDEX.md` |

**NO mezcles vaults de otros proyectos.** Los `.md` del vault de Tiendas Nachis solo aplican a ese proyecto.

### Contrato de nota (SCHEMA OBLIGATORIO)

Toda nota nueva o actualizada DEBE empezar con este frontmatter YAML (antes de cualquier texto), y el archivo DEBE guardarse en **UTF-8 con BOM**:

```yaml
---
source_path: Views/punto de venta/1.0_Punto_de_venta.xaml.cs
symbols: [PuntoDeVentaView, OnMesasClick]
tags: [pos, ui, mesas]
related: ["[[MainWindow.xaml.cs]]", "[[BarraLateral.xaml]]"]
status: verified   # verified | wip | deprecated
---
```

* `source_path`: ruta REAL del archivo de código relativa a la raíz de CÓDIGO (`sabores wanka\`), resuelve el problema de notas homónimas en carpetas distintas.
* `symbols`: métodos/clases/props clave tocados.
* `tags`: conceptos (ej. `pos`, `ui`, `mesas`, `mojibake`) para búsqueda semántica manual.
* `related`: enlaces `[[ ]]` bidireccionales al resto de notas afectadas.
* `status`: estado de la solución.

### Reglas de negocio críticas (cache)

- **POS / mesas / historial de ventas**: reglas y decisiones en `sabores wanka\CONTEXTO.md` (LEER ANTES de tocar código de esas pantallas).
- **Encoding**: si aparece mojibake, seguir `Historial_Sabores_Wanka\MojibakeRegistry.md`.

---

## ⚙️ Codebase-Memory (índice de grafos)

- Proyecto canónico POS C# → `sabores-wanka-pos`.
- Al reindexar (`index_repository`), pasar SIEMPRE `name: sabores-wanka-pos` — sin override se crea un DUPLICADO.
- **Reindexado incremental:** usar `mode: fast` o `detect_changes` (git diff / working tree) — NUNCA `full` por defecto. Si el archivo ya está en contexto de la sesión, no reindexar por cambios pequeños (R2).
- Si `search_graph`/`index_status` no muestran los cambios recientes → reindexar ANTES de explorar (la desactualización no excusa el grep).
- Uso: `search_graph`/`trace_path` para ubicar nodos y callers; `get_architecture` para el mapa de capas; `query_graph` para análisis multi-hop (hot paths, deuda).
- Reglas completas de tokens/memoria (R1-R4) en `PROTOCOLO.md`.

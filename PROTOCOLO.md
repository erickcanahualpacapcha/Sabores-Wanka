# PROTOCOLO.md — Proceso de trabajo (ciclo, fases, gate, specs, ADRs)

> Archivo de PROCESO del repo. Contiene TODO lo procedimental (ciclo de edición,
> protocolo senior, gate de bitácora, spec-first, ADRs, mojibake).
> AGENTS.md contiene solo lo OPERATIVO (rutas, comandos, reglas de negocio, memoria).
> Si editas AGENTS.md, deja el proceso aquí. Si editas aquí, no dupliques reglas en AGENTS.md.

---

## 🧭 CICLO COMPLETO DE 7 PASOS (ESTÁNDAR OBLIGATORIO)

Toda operación de edición de código debe seguir ESTRICTAMENTE este ciclo. Saltarse un paso requiere justificación explícita en el mensaje de commit/log.

```
┌────────────────────────────────────────────────────┐
│  1. Leer CONTEXTO.md del proyecto                  │
│     → reglas de negocio (POS, mesas, historial)    │
├────────────────────────────────────────────────────┤
│  2. Leer specs/ del feature                        │
│     → contrato: qué debe hacer, casos borde,       │
│       criterios de aceptación                      │
│     → specs/feature/index.md mapea source_path     │
│       a la spec correspondiente                    │
├────────────────────────────────────────────────────┤
│  3. Leer INDEX.md del vault Obsidian               │
│     → errores previos, lecciones aprendidas        │
│     → localiza nota por source_path del archivo    │
├────────────────────────────────────────────────────┤
│  4. ANÁLISIS DE IMPACTO (backlinks)                │
│     → grep "[[X.cs]]" en todo el vault             │
│     → cada nota que enlace a X puede romperse      │
├────────────────────────────────────────────────────┤
│  5. codebase-memory search_graph / trace_path      │
│     → encontrar exactamente dónde tocar            │
│     → entender call graph, dependencias,           │
│       puntos de entrada                            │
├────────────────────────────────────────────────────┤
│  6. EDITAR CÓDIGO + TESTS                          │
│     → seguir encoding rules (UTF-8 BOM, CRLF)      │
│     → tests primero si aplica (TDD)                │
│     → verificar con dotnet test                     │
├────────────────────────────────────────────────────┤
│  7. FASE WRITE (retroalimentar al grafo)           │
│     → crear/actualizar nota en Obsidian vault      │
│       con frontmatter completo                     │
│     → actualizar INDEX.md del vault                │
│     → si la spec quedó obsoleta → actualizarla     │
│     → log con fecha, archivos, error, solución     │
└────────────────────────────────────────────────────┘
```

**REGLAS DEL CICLO:**

1. **NUNCA edites código sin haber leído la spec + el vault primero.** La spec define el QUÉ; el vault define el POR QUÉ NO hacerlo de cierta forma.
2. Si un paso no aplica (ej. no existe spec para ese feature), justifícalo y continúa.
3. El paso 5 (codebase-memory) es obligatorio para cambios que toquen >1 archivo o lógica de negocio.
4. El paso 7 (Fase Write) es obligatorio para TODA edición, incluso fixes triviales — una línea sin nota es una lección perdida.
5. Si detectas mojibake durante cualquier paso, detente y sigue el protocolo en `MojibakeRegistry.md`.

---

## ⚡ PROTOCOLO DE EJECUCIÓN SENIOR (5 FASES — CERO ALUCINACIÓN)

> Estándar OBLIGATORIO. Absorbe los pasos 1-5 del ciclo de 7 pasos.
> Resultado medido en el proyecto hermano (benchmark real): -83% tool calls, -97% tokens
> de exploración, +40% cobertura de callers, 100% correlación grafo↔código,
> 0 builds rotos por impacto no mapeado.
> Nota espejo en vault: `[[ProtocoloEjecucionSenior]]`.

**Proyecto en codebase-memory (usar este nombre):**
- POS C# → `sabores-wanka-pos`

### FASE 1: NAVEGACIÓN ESTRUCTURAL (Graph-First)
- Ejecuta `search_graph` y `trace_path` en codebase-memory para ubicar los nodos afectados y sus callers transitivos (inbound/outbound).
- **PROHIBIDO** usar grep o leer archivos completos de más de 200 líneas para EXPLORAR.
- Si el índice está desactualizado (`index_status` o `search_graph` no muestra tus cambios recientes), ejecuta `index_repository` ANTES de continuar — la desactualización NO es excusa para volver a grep.
- **REINDEXADO INCREMENTAL:** si reindexas, usa `mode: fast` (filtra archivos) o `detect_changes` sobre el working tree — NUNCA `full` por defecto. Si el cambio es pequeño y el archivo ya está en contexto de la sesión, NO reindexar: leer directo es suficiente (R2).
- Al reindexar con `index_repository`, pasar SIEMPRE `name: sabores-wanka-pos` — sin el override el nombre derivado crea un DUPLICADO del índice.

### FASE 1.5: CLASIFICACIÓN DEL ESTADO (obligatoria antes de Fase 2)
- Después del diagnóstico, clasifica el estado en UNA de estas dos ramas:
- **RAMA A — ANOMALÍA CRÍTICA:** existe un test fallido, un error de compilación o una regresión verificable en la lógica de negocio. → Ejecuta Fase 2 (Especificación) → Fase 3 (Cirugía atómica).
- **RAMA B — BASELINE ESTABLE:** todos los tests pasan, la memoria está operativa, pero existen deudas de calidad de datos (encoding, schema, enlaces, duplicados). → Ejecuta **FASE 2-ALT: Sanitización dirigida de deuda técnica**:
  - PROHIBIDO tocar lógica de negocio o código con tests verdes.
  - PROHIBIDO migrar notas en masa (violación de MIGRACIÓN LAZY).
  - Permitido: corregir mojibake real (datos corruptos, nota por nota), implementar validators en caliente (`BovedaTest/scripts/validate-note.ps1`), lazy link repair (`lazy-link-repair.ps1`) y dedup de INDEX al reindexar (`dedup-index.ps1`).
- **Regla de oro:** si no hay test roto, no hay cirugía de código.

### GATE OBLIGATORIO DE LA BITÁCORA (vigente SIEMPRE)
- **Regla:** "Nota sin frontmatter completo = NO entra al grafo".
- Antes de `index_repository` o de cerrar cualquier Fase Write, corre:
  ```powershell
  powershell -ExecutionPolicy Bypass -File "BovedaTest\scripts\gate-validate.ps1" -VaultPath "Historial_Sabores_Wanka"
  powershell -ExecutionPolicy Bypass -File "BovedaTest\scripts\gate-validate.ps1" -VaultPath "Historial_Sabores_Wanka" -Strict
  ```
- Exit 0 = se puede indexar; exit 1 = BLOQUEADO (corrige la nota antes de indexar).
- Modo normal: exige 5 campos frontmatter + BOM + sin FFFD; related rotos = warning (deuda).
- Modo `-Strict`: TODO bloquea (related rotos, sin frontmatter, cualquier FAIL).
- `validate-note.ps1` valida una nota individual antes de escribirla (exit 0/1).

### FASE 2: ESPECIFICACIÓN Y ANÁLISIS DE IMPACTO
- Mapea el árbol de dependencias por capas (UI, Servicios, Datos).
- Si un cambio rompe compatibilidad con llamadas existentes, diseña un **overload** o método defensivo en lugar de romper firmas.
- Lee la spec (`specs/`) y la nota del vault (`INDEX.md` → nota por `source_path`) ANTES de diseñar.

### FASE 3: CIRUGÍA ATÓMICA DE CÓDIGO
- Usa grep o lectura puntual ÚNICAMENTE para localizar las líneas exactas a modificar (ya tienes el mapa del grafo).
- Cambios mínimos, limpios y estrictamente tipados.
- Preserva codificación (UTF-8 BOM, CRLF) y convenciones existentes.

### FASE 4: VERIFICACIÓN Y TEST RUNNER
- Ejecuta `dotnet build` y, cuando exista proyecto de tests, `dotnet test`.
- Cero tolerancia: 100% de tests deben pasar. Si un test falla por un caller oculto, el grafo te dijo que existía — revisa el árbol antes de tocar código.

### FASE 5: DOCUMENTACIÓN Y CIERRE (Vault)
- Actualiza las notas de Obsidian + `INDEX.md` del vault (schema con frontmatter obligatorio).
- Si se ejecutó RAMA B, documentar el baseline actual (`tests pass`, `% de deuda`, `validators activados`) en `BovedaTest/resultados/` y crear/actualizar el ADR correspondiente (`adrs/`).
- Presenta al usuario: resumen de cambios, árbol de impacto verificado y resultado de los tests.

---

## 📐 SPEC-FIRST WORKFLOW

Toda feature nueva o bug fix DEBE empezar con un archivo `.spec.md` en `specs/`:

- **Ubicación**: `specs/<categoria>/<feature>.spec.md`
- **Formato**: Frontmatter YAML + Contrato (Qué hace, Input/Output/Excepciones, Casos borde, Criterios de aceptación)
- **Sin implementación**: la spec describe el QUÉ, no el CÓMO
- **Ciclo de vida**: `draft → review → active → implemented → stale`
- **INDEX**: `specs/index.md` mapea cada spec a sus source_path

Regla: si una feature tarda >1 iteración en implementarse, la spec se actualiza ANTES de cada iteración, no después.

---

## 📋 ADRS (ARCHITECTURE DECISION RECORDS)

Las decisiones arquitectónicas importantes se registran en `adrs/`:

- **Ubicación**: `adrs/numero-titulo.adr.md`
- **Formato**: MADR (Markdown Any Decision Records) simplificado:
  - Título, Estado, Contexto, Decisión, Consecuencias
- **Cuándo crear**: cuando una decisión afecta a >1 servicio, cambia el modelo de datos, o introduce una deuda técnica consciente
- **INDEX**: `adrs/index.md` lista todos los ADRs

---

## 📝 FASE READ / FASE WRITE (schema de nota y log)

**FASE READ — antes de modificar `X.xaml.cs` / `X.cs` / `X.xaml`:**
0. Lee `CONTEXTO.md` del proyecto — reglas de negocio criticas en cache local.
1. Abre el `INDEX.md` del vault y localiza la fila cuyo `source_path` coincide con el archivo a editar → obtén la nota exacta (NO adivines el nombre).
2. Lee esa nota completa. DESCARTA de inmediato las rutas/soluciones marcadas como fallidas o `deprecated`.
3. **ANÁLISIS DE IMPACTO (backlinks):** ejecuta una búsqueda `grep "\[\[X.cs\]\]"` sobre TODO el vault. Cada nota que enlace a `X` es un archivo que puede romperse con tu cambio. Léelas y advierte del impacto ANTES de codificar.
4. Si NO existe nota ni entrada en INDEX, busca por `tags` conceptuales antes de asumir que no hay memoria.

**FASE WRITE — después de arreglar un bug o aprender una regla:**
1. Crea/actualiza la nota espejo **con el frontmatter completo** del schema de arriba.
   * **REGLA DE CARPETAS ESTRICTA:** guarda la nota bajo su categoría real (`Views/`, `Models/`, `Services/`, `ui/`, `data/`, etc.). NUNCA crees subcarpetas con el nombre del proyecto.
2. Añade/actualiza la fila correspondiente en el `INDEX.md` del vault (`source_path | nota | tags`).
3. **REGLA DE ENLACES (GRAFO):** en `related` y en el log, enlaza con `[[ ]]` TODOS los archivos afectados. Los enlaces deben ser bidireccionales.
4. Añade la entrada de log al final de la nota con el formato de abajo.

**MIGRACIÓN LAZY:** si tocas un archivo cuya nota existe pero NO tiene frontmatter, añádeselo en ese momento y regístrala en el INDEX. No migres notas en masa.

Usa ESTRICTAMENTE este formato para cada entrada de log al final del archivo `.md` (debajo del frontmatter):
---
## [FECHA Y HORA]
* **Archivos Relacionados:** [Usa SIEMPRE los corchetes dobles aquí, ej: [[MainWindow.xaml.cs]], [[BarraLateral.xaml]]]
* **Línea modificada:** [Línea aprox]
* **El Error / Bucle:** [Qué estaba fallando o qué intentaste antes que no funcionó]
* **La Solución / Regla:** [Por qué funciona la nueva solución. Qué NO se debe volver a intentar]

---

## ⚡ REGLAS DE OPTIMIZACIÓN DE TOKENS Y MEMORIA (OBLIGATORIAS en sabores-wanka-pos)

1. **REINDEXADO INCREMENTAL (R1):** al usar `index_repository`, ejecutar SIEMPRE indexación basada en los archivos modificados (git diff / working tree) respecto al grafo base — `mode: fast` o `detect_changes` primero. NUNCA procesar el repositorio completo desde cero (`full`) salvo pedido explícito o cambio masivo.
2. **CONTEXTO ACTIVO (R2):** mantener en la memoria de la sesión los archivos en edición activa. NO sugerir reindexar por pequeños cambios si la lectura directa del archivo en contexto es suficiente. El grafo es para EXPLORAR; el contexto es para EDITAR.
3. **FRESCURA OBSIDIAN PRIORITARIA (R3):** en cuanto se resuelva un bug o se implemente una función clave, redactar DE INMEDIATO la nota en el vault `.md` con su frontmatter completo y agregarla al INDEX.md. La memoria documental tiene prioridad sobre consultas al grafo (el vault es más barato de leer y sobrevive a la sesión).
4. **VALIDACIÓN RUNTIME (R4):** antes de dar por cerrado un cambio que toque base de datos o lógica de sincronización, revisar los logs de runtime para evitar puntos ciegos que ni el grafo ni el vault detecten. Cerrar solo con evidencia de logs.

---

## ⚔️ Mojibake

Si detectas mojibake (caracteres corruptos como `Ã¡`, `Ã±`, `â€™`, U+FFFD):
1. Revisa **[[MojibakeRegistry]]** para el protocolo completo
2. Registra la correccion alli mismo

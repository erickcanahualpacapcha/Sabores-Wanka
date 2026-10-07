# CONTEXTO.md — Sabores Wanka (reglas de negocio en cache)

> Cache local de reglas de negocio y decisiones criticas del POS.
> LEER ANTES de tocar codigo de las pantallas que aqui se describen.
> Este archivo es la fuente rapida; el detalle historico vive en el vault
> `Historial_Sabores_Wanka\` (INDEX.md -> nota por source_path).

---

## Estado del proyecto

App WinUI 3 (Windows App SDK 2.5.1), .NET 8, namespace `sabores_wanka`.
Es un POS de escritorio en fase inicial: shell de ventana con barra lateral
y dos secciones (Punto de Venta con mesas, e Historial de Ventas).

Ruta real del codigo: `C:\Users\Erick\Desktop\sabores wanka\sabores wanka\`

## Navegacion

- `MainWindow.xaml.cs` cablea `MainSidebar` (`Views.Components.BarraLateral`)
  y `MainContent`. Los eventos `PuntoDeVentaSelected` / `HistorialSelected`
  sustituyen `MainContent.Content` (sin Frame/NavigationService todavia).
- Cada seccion vive en su carpeta de `Views/` con su namespace respectivo.

## Reglas de UI

- Al replicar disenos exportados de Stitch (`code.html`, `screen.png`,
  `stitch_*.zip`), priorizar `Grid` + `VisualStateManager` sobre posiciones
  absolutas; mantener `x:Name` en PascalCase y eventos `XxxSelected`.
- Separacion UI/logica: la vista no debe contener reglas de negocio complejas
  cuando aparezcan Services/Models; moverlas al servicio correspondiente.

## Persistencia local de platos (2026-10-06)

- Archivo: `%LocalAppData%\SaboresWanka\Data\platos.json` (patron Nachis
  simplificado: full local, sin SQLite ni nube). Semilla de 8 platos.
- `PlatosStore`: `ListarAsync`, `AgregarAsync` (duplicado por nombre
  normalizado y PLU unico), `ActualizarStockAsync`, `SiguientePluAsync`.
- Solo unidades/porciones enteras. Estado: 0 Agotado, <=5 Stock Bajo,
  resto En Carta. Montos aceptan punto o coma decimal.
- Punto de venta: al abrir suma del JSON los platos con stock > 0 que no
  existan por nombre (badge compactado a PLATO/CALDO/ENTRADA/BEBIDA).
  Buscador y pills del POS filtran ese mismo listado sin perder badges.
- Punto de venta sin mesa activa: catálogo en plomo (`PlatoGrid.Opacity 0.4`
  + `VeloSinMesa` + pill `AvisoSinMesa`); el tap muestra el toast glass
  `SinMesaToast` y no agrega nada. Mesa activa = `_mesaActual` no vacío +
  picker cerrado y sin cierre. Al abrir, `_mesaActual` es "" ("Sin mesa",
  pill en gris); al cobrar la mesa completa se resetea a sin mesa y vuelve
  el picker.
- La mesa actual vive en la píldora centrada de la barra de título
  (`MainWindow.MesaTitlePill`, solo visible en POS, recortada del arrastre
  para ser clicable y abrir el selector). El POS avisa por evento
  `MesaActualCambiada`; el panel inferior ya no tiene selector de mesa.
- Ventas abiertas sobreviven cierres y cortes de luz: `VentasPendientesStore`
  (`ordenes_activas.json`) guarda mesa actual + órdenes con items en cada
  cambio del carrito; al abrir se restauran, las mesas quedan OCUPADA y se
  vuelve a la mesa atendida. Sin nada que guardar se borra el archivo.
- Ventas cerradas: `VentasStore` (`ventas.json`, serie B001-#######). Cada
  confirmación de cierre = 1 boleta (total o parcial). Historial/Ingresos y
  sus KPIs se pintan desde ese JSON (solo PAGADO suma).
- Venta a credito (2026-10-06): solo el metodo `Crédito` crea `POR COBRAR`
  (comparacion OrdinalIgnoreCase con/sin tilde); Efectivo/Tarjeta/Yape/Plin/
  Otros crean `PAGADO`. Ingresos = solo PAGADO (por `FechaEfectiva` =
  `FechaCobro ?? FechaHora`); Por cobrar = POR COBRAR + EN MESA legado.
  Cobrar (`VentasStore.MarcarCobradaAsync`) pide metodo de cobro, fija
  `Estado=PAGADO` + `FechaCobro=now` y la boleta pasa a Ingresos del dia
  del cobro. KPIs/badges por periodo usan el mismo criterio.
- Gastos de caja: `GastosStore` (`gastos.json`, serie EG-0000xx) con vista
  `2.0_gastos` (nombre, tipo, responsable, nota, valor, método). Egresos y
  sus KPIs + Balance se pintan desde ese JSON (Balance = ventas − gastos).
- Periodo del reporte (patrón Nachis simplificado): ComboBox
  Diario/Semanal/Mensual/Anual + calendario propio `CalendarioPeriodoControl`
  (días con semana resaltada, solo meses, solo años). Filtra filas y KPIs de
  ventas, gastos y balance.
- Edición de platos: el lápiz abre el formulario 2.0 precargado
  (`CargarParaEdicion`) y guarda con `PlatosStore.ActualizarAsync` por el
  mismo PLU (el duplicado de nombre ignora su propio PLU).
- Edición de ventas (2026-10-07, patrón Nachis adaptado): el lápiz de
  Ingresos y Por cobrar abre `2.0_editar_venta` flotante (`VentaEdicionView`,
  mismo overlay `GastoOverlayHost`, arrastrable) precargado con cliente,
  mozo, comprobante, método e items (cantidad +/-, precio, quitar, total
  vivo). Solo boletas de últimos 30 días (regla Nachis `CanEditSale`).
  Guarda con `VentasStore.ActualizarAsync` por ticket (nunca cambia Estado
  ni fechas; la deuda se cobra solo con Cobrar) y ajusta stock por la
  diferencia de cantidades (mejor esfuerzo).
- El PLU es interno y automático (nunca se muestra); en el formulario hay
  "Tipo de Producto" (Porción/Plato/Vaso/Jarra/Copa/Botella/Unidad/Taza)
  que define la unidad del stock ("35 platos", "15 vasos").
- Fotos: picker del sistema + vista previa en el formulario; al guardar se
  copian a `%LocalAppData%\SaboresWanka\Imagenes\Platos` (`FotosPlatos`) y
  la ruta va en `Plato.FotoPath`. El POS muestra la foto local en la tarjeta
  y en la miniatura del carrito (fallback a PNG por slug e icono).
- Stock vivo en el POS: `STOCK: inicial − reservado` (todas las mesas
  abiertas) con `x:Bind OneWay`. Al cobrar se descuenta del JSON del
  catálogo (los 12 de la casa son demo fija sin persistencia).
- Sobregiro permitido (Nachis AllowOverdraft): el stock puede dar negativo
  (pill en rojo) y la venta nunca se bloquea. `Pausado` es flag aparte
  (no toca el stock) y es lo único que saca de carta.
- WhatsApp estilo Nachis: vista con WhatsApp Web embebido (`WebView2`, QR una
  vez, sesión persistente) + `WhatsappEnlace` que abre `wa.me` con la boleta
  redactada. Sin motor Node, sin bot ni estados de envío.

## Encoding (regla dura)

- Codigo y notas en **UTF-8 con BOM**; JSON sin BOM. CRLF, 4 espacios.
- Nunca copiar texto desde la salida de PowerShell a comentarios o strings.

## Pendientes / decisiones abiertas

- Capa `Services/` y `Models/` creada el 2026-10-06 solo para platos
  (`PlatosStore` JSON local + `Plato`). Punto de venta e historial siguen mock.
- No existe proyecto de tests. Cuando se cree, sera xUnit + Moq en un
  `.csproj` aparte con guard de arquitectura.
- A medida que se implementen reglas (ventas, mesas, cobros, impresion),
  documentarlas AQUI y en el vault el mismo dia (regla de frescura).

# Glosario

Vocabulario del proyecto. Se completa durante la entrevista inicial.

- **FIT**: documento Excel con todas las features de una pieza para la plataforma I4 de GESTAMP. Hasta ahora lo generaba GeneradorFIT a partir de una plantilla Excel rellenada a mano; esta app crea esos datos desde la propia app.
- **Proyecto**: conjunto de trabajo de la app; tiene una planta, una línea y una o más referencias.
- **Planta** (`Plant`): código de la planta (columna A de la plantilla, p. ej. `LAP`).
- **Línea** (`Line`): código de la línea (columna B, p. ej. `LE5`).
- **Referencia** (`PartReference`): cada pieza que fabrica la línea, numerada 1, 2… En la plantilla, una hoja `PLC-RefN` por referencia.
- **Feature**: cada característica de la pieza que se traza (un cordón, un tack, un perno, un agujero…), con su FeatureReferenceClient, tipo, location, FeatureId y si es crítica.
- **Fixture / LayoutName (Asset) / Working**: utillaje donde se hace la feature, el robot o puesto que la hace y su número de trabajo. Una feature puede tener uno o más.
- **Tipo de feature** (`Process | FeatureType`): fila de la tabla de tipos (hoja `Feature Type Table`), con número, proceso, detalle y tecnología de PLC.
- **Tecnología** (`Tech`): número de tecnología del PLC (Arco, Marcadora, DMC…) asociado a cada tipo de feature.
- **Pointer / Main Pointer / Offset**: posición de la feature dentro de su tecnología, posición global y código `RRTTPPP` (referencia, tecnología, pointer).
- **Location**: puesto con nombre y número; en el FIT se escribe el número.
- **Screen Names**: nombres de las 24 pantallas de trazabilidad del HMI.
- **Control de versiones**: lista de cambios del proyecto con fecha y descripción (hoja `Control Versiones`).

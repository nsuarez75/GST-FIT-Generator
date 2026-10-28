# Glosario

Vocabulario del proyecto. Se completa durante la entrevista inicial.

- **FIT**: documento Excel con todas las features de una pieza para la plataforma I4 de GESTAMP. Hasta ahora lo generaba GeneradorFIT a partir de una plantilla Excel rellenada a mano; esta app crea esos datos desde la propia app.
- **Proyecto**: conjunto de trabajo de la app; tiene un nombre (p. ej. `ABC_XYZ`, da nombre a los Excel generados), una planta, una línea y una o más referencias (máximo 32).
- **Auxiliar**: feature que no pertenece a ninguna referencia (botoneras, HMIs), con Asset, Working y FeatureId (hoja `PLC-Aux`). Sale en el FIT con PartReference 99 y al final de cada `TableInfo`; no cuenta para pointers.
- **Tamaños**: número de features más reservas de cada referencia y tecnología; es el valor de las constantes `TechNN_Ref_MM` de TIA que dimensionan los arrays.
- **Planta** (`Plant`): código de la planta (columna A de la plantilla, p. ej. `PLT`).
- **Línea** (`Line`): código de la línea (columna B, p. ej. `L01`).
- **Referencia** (`PartReference`): cada pieza que fabrica la línea, numerada 1, 2… En la plantilla, una hoja `PLC-RefN` por referencia.
- **Feature**: cada característica de la pieza que se traza (un cordón, un tack, un perno, un agujero…), con su FeatureReferenceClient, tipo, location, FeatureId y si es crítica.
- **Fixture / LayoutName (Asset) / Working**: utillaje donde se hace la feature, el robot o puesto que la hace y su número de trabajo. Una feature puede tener uno o más.
- **Tipo de feature** (`Process | FeatureType`): fila de la tabla de tipos (hoja `Feature Type Table`), con número, proceso, detalle y tecnología de PLC.
- **Tecnología** (`Tech`): número de tecnología del PLC (Arco, Marcadora, DMC…) asociado a cada tipo de feature.
- **Pointer / Main Pointer / Offset**: posición de la feature dentro de su tecnología, posición global y código `RRTTPPP` (referencia, tecnología, pointer).
- **Reserva**: feature comodín de una tecnología («Reserva de <tecnología>») que ocupa un pointer para poder añadir features nuevas sin que se muevan las demás. No sale en el FIT, el FB ni CheckTechTemplate; en las listas del HMI sale con el texto `Reserva`. Se convierte en feature real cambiándole el tipo.
- **Catálogo de tipos de feature**: la tabla de tipos de feature, común a todos los proyectos. La lista de tecnologías, la tecnología de cada tipo y las locations van en cada proyecto.
- **Location**: puesto con nombre y número; en el FIT se escribe el número.
- **Screen Names**: nombres de las 24 pantallas de trazabilidad del HMI. El nombre de la pantalla n es `<prefijo>_<número+n−1>_Traceability_NN`; la descripción es el subtítulo de la pantalla (lista `CO_Traceability_ID`).
- **Pantalla de techs**: pantalla de trazabilidad del HMI con un faceplate `TechLayout` por feature, que enseña el estado de su tecnología. La genera la app como XML para importar en TIA.
- **Recuadro**: grupo de una pantalla de techs con las features de un Fixture (o, en la pantalla de reservas, las reservas de una referencia y tecnología). Dentro, una columna por Asset.
- **Pantalla de inicio**: primera pantalla de techs de una referencia (o de las reservas). Si la referencia anterior no cabe, la siguiente se corre a la primera libre.
- **Nombre corto**: nombre de un Asset o Fixture para las pantallas de techs (p. ej. `R10.1` en vez de `1000R1`). Si no tiene, se usa el real.
- **Control de versiones**: lista de cambios del proyecto con fecha y descripción (hoja `Control Versiones`).

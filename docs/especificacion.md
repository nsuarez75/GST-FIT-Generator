# Especificación de GST-FIT-Generator

Acordada en la entrevista del 2026-10-27. El vocabulario está en `GLOSSARY.md`.

## Objetivo

App de escritorio en .NET 10 con Avalonia donde se crean los datos del FIT de una pieza (proyecto, referencias, features, tecnologías, locations, auxiliares, Screen Names y control de versiones) y que, a partir de ellos, genera:

- La **plantilla Excel** que hasta ahora se rellenaba a mano, con todas sus hojas y las columnas calculadas (V-Z y AD-AK).
- Todo lo que generaba GeneradorFIT V3.0.1 a partir de esa plantilla: el **Excel FIT** para la plataforma I4 de GESTAMP, `User_TableInfo_DB.db`, `User_Config_Reference_FB.awl`, `CheckTechTemplate_DB.db` y los `CheckTechTemplate_DB_Init_<fixture>.scl`.

GeneradorFIT queda en desuso, congelado en la V3.0.1.

## Interfaz

- Sigue el estándar de interfaz de las apps nuevas, tomado de GST-LoS-Generator: mismo estilo, barra superior con iconos, tema claro, oscuro o según el sistema, idioma castellano o inglés, aviso de versión nueva, deshacer y rehacer, y aviso al cerrar con cambios sin guardar.
- Ejecutables para Windows y Linux.
- Panel izquierdo con el árbol del proyecto: datos del proyecto, tecnologías, locations, referencias, auxiliares, pantallas (un solo menú con Screen Names y Pantallas de techs), control de versiones y tamaños.

## Archivos

- Proyecto: un archivo `.fitproj` que se guarda donde se quiera y se puede exportar e importar.
- Catálogo de tipos de feature: común a todos los proyectos. Se exporta e importa.
- Salida: carpeta elegida por proyecto y recordada. Cada casilla escribe en su subcarpeta: `Plantilla/`, `FIT/`, `TableInfo_DB/`, `Config_Reference_FB/`, `CheckTech_DB/` y `Pantallas/`.
- La plantilla generada se llama `<nombre>_FIT_rev<N>_<fecha>.xlsx`, donde N es el número de revisión que el usuario pone a mano en el proyecto (al importar una plantilla se toma de su nombre), y el Excel FIT se llama `<nombre>_FIT_<fecha>.xlsx`.

## Proyecto

- Tiene un nombre (por ejemplo `ABC_XYZ`), una planta (`Plant`, columna A) y una línea (`Line`, columna B).
- Admite hasta 32 referencias, que es el máximo del FB.
- Tecnologías del proyecto: de la 0 a la 10, con el nombre editable. La 0 es «No technology» y la 10 es la última posible. Dan la lista `CO_Traceability_Tech` (AJ-AK).
- Cada tipo de feature del catálogo tiene, dentro del proyecto, el número de tecnología que le corresponde.
- Locations del proyecto: nombre y número. Al editar features se elige por nombre; en lo generado se escribe el número.

## Catálogo de tipos de feature

- Es la hoja `Feature Type Table`: número, proceso y detalle. En las features aparece como `Process | Detail`.
- Se puede crear y modificar desde la app.

## Referencias y features

- Cada referencia (`PartReference` 1, 2…) tiene su lista de features.
- Campos de una feature: uno o más grupos Fixture / LayoutName (Asset) / Working (sin límite), FeatureReferenceClient, tipo de feature, location, FeatureId y CriticalFeature.
- Edición en una tabla tipo Excel: una fila por feature, con desplegables para el tipo y la location y una casilla para CriticalFeature. Los grupos de fixture se editan en un panel lateral al seleccionar la fila. Al hacer doble clic se abre la feature en un formulario (si no complica en exceso; si no, solo tabla).
- Una feature nueva se coloca justo detrás de la seleccionada y copia de ella los fixtures, el tipo y la location.
- Se pueden pegar varias filas copiadas de un Excel para crear features de golpe.
- El FeatureId se propone a partir del FeatureReferenceClient (`W69` → `10069`, `W73_01` → `17301`) y se puede editar. Se avisa si un FeatureId se repite dentro de la misma referencia.
- Botón «Duplicar referencia»: crea una referencia nueva con todas las features, reservas incluidas.

## Orden, pointers y offsets

- Las features de cada referencia se agrupan por tecnología. Dentro de cada tecnología mantienen el orden de la lista.
- Por cada feature se calcula:
  - **Pointer**: su posición dentro de su tecnología y su referencia.
  - **Main Pointer**: su posición global en la referencia.
  - **Offset**: `RRTTPPP` (referencia, tecnología y pointer).
- **Los pointers nunca cambian solos.** Solo cambian cuando el usuario intercala, arrastra o borra una feature.
- La tabla muestra un bloque por tecnología, con el pointer y el offset visibles en todo momento. Las features se reordenan arrastrándolas dentro de su bloque; para pasar una a otra tecnología se le cambia el tipo.
- Al cambiar el tipo de una feature a otra tecnología, la feature pasa al final del bloque de esa tecnología y la app avisa de que se desplazan pointers.
- Al borrar una feature, la app pregunta: «Convertir en reserva (no se mueve nada)» (opción por defecto) o «Borrar del todo (se desplazan N features)».
- Al generar, si algún offset ha cambiado desde la última generación, la app enseña la lista de cambios antes de escribir los archivos.

## Reservas

- Son un tipo comodín que se elige a mano: «Reserva de <tecnología>».
- Cuentan para pointers, offsets y tamaños.
- No salen en el Excel FIT, en el FB ni en `CheckTechTemplate`. En las listas del HMI (AD-AK) salen con su offset, el texto `Reserva`, FeatureId 0 y sin LayoutName.
- Una reserva se convierte en feature real cambiándole el tipo. Si el tipo nuevo es de la misma tecnología, conserva su pointer.

## Tamaños

- Tabla de referencias × tecnologías con el número de features más reservas de cada casilla y el nombre de la constante de TIA que dimensiona el array (`Tech02_Ref_01 = 45`).
- Botón «Copiar» para llevarla a TIA.

## Auxiliares

- Equivalen a la hoja `PLC-Aux`: Asset, Working y FeatureId.
- Salen al final del Excel FIT, con PartReference 99, y al final de cada `TableInfo`.
- No cuentan para pointers, HMI, FB ni tamaños.

## Screen Names

- 24 filas con nombre y descripción.
- El nombre se rellena a partir de un prefijo del proyecto, que es el nombre de la primera pantalla sin `_Traceability_01` (p. ej. `CO_0000_100`). Como en TIA, el número del final del prefijo aumenta en uno en cada pantalla: `CO_0000_100_Traceability_01`, `CO_0000_101_Traceability_02` … `CO_0000_123_Traceability_24`. La descripción se escribe a mano.
- Van a la hoja `Screens Names` de la plantilla, de donde sale la lista de textos `CO_Traceability_ID` del HMI: el subtítulo de cada pantalla de trazabilidad.

## Pantallas de techs

Acordado en la entrevista del 2026-10-28.

- Pantallas de trazabilidad del HMI (WinCC Comfort/Advanced) con un faceplate `TechLayout` por feature, que enseña el estado de su tecnología. Se generan como XML para importar en TIA, una por pantalla, en `Pantallas/`.
- En el árbol, «Pantallas de techs»: la **pantalla de inicio** de cada referencia y de las reservas (o «No»), los **nombres cortos** de Fixtures y Assets, los avisos del reparto y una **vista previa** de cada pantalla.
- Recuadros: uno por Fixture, para todas las tecnologías, en el orden de la lista de features. El título es el Fixture o su nombre corto. Dentro, una columna por Asset, ordenadas por nombre de Asset; las filas, por Main Pointer. Una columna demasiado larga sigue en otra al lado; un recuadro más ancho que la pantalla sigue en otro con «(cont.)».
- Cada fila: el texto «Asset - FeatureReferenceClient» (con el nombre corto del Asset si lo tiene) y su faceplate, con `Status = Tech_Status_<Main Pointer>`, `Position_tech_in = Int_Value_<Pointer>`, `Number_tech_in = Int_Value_<tecnología>` y `Reference_in = Int_Value_<referencia>`. Las salidas `Status_Out`, `Number_Tech_out`, `Pos_Tech_out` e `ID_out` van a `GlobalStatus`, `Form_TECH`, `Form_POS` y `Form_ID`.
- Una feature hecha en varios Fixtures sale una sola vez, en el primero: la característica existe una vez en la pieza (los demás son estaciones duplicadas). Las de tecnología 0 no salen; las que no tienen Fixture van al recuadro «Sin fixture», con aviso.
- Reparto: cada referencia llena pantallas desde su pantalla de inicio, y cada recuadro va en el hueco libre más arriba y a la izquierda; si no cabe, se prueba con columnas más bajas y, si aun así no cabe, pasa a la pantalla siguiente. Un recuadro no se parte entre pantallas. Si una referencia necesita más pantallas y pisa la de inicio de la siguiente, la siguiente se corre a la primera libre, con aviso. Pasar de la 24 es un error y no se genera.
- Reservas: solo salen si tienen pantalla de inicio; todas las referencias la comparten, con un recuadro por referencia y tecnología y el texto «Tecnología - Pointer».
- Al generar, la descripción vacía de Screen Names de cada pantalla usada se rellena con «RefN Techs» o «Reservas».
- La parte fija de cada pantalla es la del estándar SICAR@GST: plantilla `CC_0000_ScreenTemplate`, eventos `ClearScreen` y `GenerateScreen` (menú), botones Leyenda, Enclavamiento e I4.0, `ScreenLocking`, cabecera y subtítulo con la lista `CO_Traceability_ID` y `Int_Value_<NN>`. Los botones de pantalla anterior y siguiente solo enlazan pantallas de la misma referencia (o de reservas).
- Estilo de cada proyecto, en el bloque «Estilo»: colores (fondo y borde del recuadro, franja y texto del título, texto de las filas) con selector de color, letra del título y de las filas (8 a 30), alto de fila (más que el faceplate, hasta 80) y separación entre recuadros (0 a 60). «Restaurar estilo por defecto» vuelve al de serie: sobrio, con recuadros claros y el título en una franja, para que destaque el estado de los faceplates. La vista previa cambia al momento.
- Lienzo (en el mismo bloque, ahora «Estilo y lienzo», plegado por defecto): los cuatro márgenes de la zona donde van los recuadros (de serie 16, 100, 16 y 126 en la pantalla de 1920×966) y el ancho del faceplate (16 a 80; el alto sale de su proporción 34×35, como permite el `TechLayout` con `KeepRatio`). La vista previa dibuja el lienzo con una línea discontinua.

### Retoques a mano (acordado en la entrevista del 2026-10-28, #36)

- El reparto sigue siendo automático; cada recuadro se puede **retocar** y lo que no se toca se sigue colocando solo, en los huecos que dejan los retocados.
- En la vista previa, un clic elige un recuadro y arrastrarlo lo mueve, ajustado a una rejilla de 8 px. El recuadro elegido abre un panel con **Pantalla** (las de su referencia y «Pantalla nueva»), **X**, **Y**, **Filas por columna** (vacío: las que quepan), **Partir antes de** un Asset y **Colocación automática** (quita los retoques del recuadro y de sus partes). «Recolocar todo automáticamente» quita todos. Todo se puede deshacer.
- Un recuadro solo va a pantallas de su referencia; «Pantalla nueva» añade una al final, y las referencias siguientes se corren con su aviso.
- Partir solo entre Assets: la parte nueva es «Fixture (cont.)» y se mueve por su cuenta.
- Si cambian las features, el recuadro retocado se queda donde está y crece; si pisa a otro retocado o se sale del lienzo, sale un aviso. También avisa de una pantalla que queda vacía.
- Los retoques se guardan en el proyecto por referencia y Fixture, con la pantalla relativa a su referencia (cambiar la pantalla de inicio se los lleva). Si el Fixture cambia de nombre pero no de contenido, el retoque sigue: se reconoce por sus features (el recuadro que comparta al menos la mitad). Si ya no hay ninguno, el retoque no se aplica y sale un aviso.
- Pendiente: no se comprueba que existan en el HMI las variables `Tech_Status_NNN` e `Int_Value_NNN` que se usan.


## Control de versiones

- Lista de cambios con fecha y descripción, que da la hoja `Control Versiones`.
- Al generar, si hay cambios desde la última generación, la app propone añadir una entrada.

## Importar una plantilla existente

- Botón «Importar plantilla Excel»: crea un proyecto a partir de una plantilla como las de hasta ahora (hojas `PLC-RefN`, `PLC-Aux`, `Screens Names`, `Feature Type Table` y `Control Versiones`).
- Los tipos de feature que no estén en el catálogo se añaden a él. Si un número ya existe con otro texto, se queda el del catálogo y la app enseña las diferencias.
- La tecnología de cada tipo y la lista de tecnologías (AJ-AK) pasan al proyecto.

## Generar

- Casillas para elegir qué se genera: las mismas que en GeneradorFIT, más «Plantilla Excel» y «Pantallas de trazabilidad (XML)».
- Plantilla Excel: una hoja `PLC-RefN` por referencia, con los grupos de fixture seguidos (tantos como haga falta, desplazando las columnas siguientes), V-Z (Feature Type, Tech, Pointer, Main Pointer, Offset) y AD-AK (las cuatro listas para el HMI). Además lleva las hojas `PLC-Aux`, `Screens Names`, `Feature Type Table` y `Control Versiones`. No hace falta que GeneradorFIT V3 pueda leerla.
- Excel FIT, `User_TableInfo_DB` (`TableInfo_1…N`, una tabla por cada columna de fixture usada), `User_Config_Reference_FB` y `CheckTechTemplate_DB` con sus init SCL: igual que GeneradorFIT V3.0.1, salvo que el número de tecnología se escribe siempre con 2 cifras (`Tech02`, `Tech10`).

## Decisiones técnicas

- Solución .NET 10: `GstFitGenerator.Core` (modelo, cálculo, lectura y escritura) con tests, y `GstFitGenerator.App` (Avalonia).
- Los tests comprueban que, con los mismos datos, lo generado coincide con GeneradorFIT V3.0.1. Los datos de prueba son inventados.
- Lo que se guarda en el proyecto es el orden de las features; los pointers se calculan a partir de él. Para poder comparar, se guardan los offsets de la última generación.
- `./checks.sh` compila con los avisos como errores y pasa los tests.

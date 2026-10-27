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
- Panel izquierdo con el árbol del proyecto: datos del proyecto, tecnologías, locations, referencias, auxiliares, Screen Names, control de versiones y tamaños.

## Archivos

- Proyecto: un archivo `.fitproj` que se guarda donde se quiera y se puede exportar e importar.
- Catálogo de tipos de feature: común a todos los proyectos. Se exporta e importa.
- Salida: carpeta elegida por proyecto y recordada.
- La plantilla generada se llama `<nombre>_FIT_rev<N>_<fecha>.xlsx`, donde N es el número de entradas del control de versiones, y el Excel FIT se llama `<nombre>_FIT_<fecha>.xlsx`.

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
- El nombre se rellena a partir de un prefijo del proyecto: `<prefijo>_Traceability_01` … `_24`. La descripción se escribe a mano.
- Solo se usan para la hoja `Screens Names` de la plantilla.

## Control de versiones

- Lista de cambios con fecha y descripción, que da la hoja `Control Versiones`.
- Al generar, si hay cambios desde la última generación, la app propone añadir una entrada.

## Importar una plantilla existente

- Botón «Importar plantilla Excel»: crea un proyecto a partir de una plantilla como las de hasta ahora (hojas `PLC-RefN`, `PLC-Aux`, `Screens Names`, `Feature Type Table` y `Control Versiones`).
- Los tipos de feature que no estén en el catálogo se añaden a él. Si un número ya existe con otro texto, se queda el del catálogo y la app enseña las diferencias.
- La tecnología de cada tipo y la lista de tecnologías (AJ-AK) pasan al proyecto.

## Generar

- Casillas para elegir qué se genera: las mismas que en GeneradorFIT, más «Plantilla Excel».
- Plantilla Excel: una hoja `PLC-RefN` por referencia, con los grupos de fixture seguidos (tantos como haga falta, desplazando las columnas siguientes), V-Z (Feature Type, Tech, Pointer, Main Pointer, Offset) y AD-AK (las cuatro listas para el HMI). Además lleva las hojas `PLC-Aux`, `Screens Names`, `Feature Type Table` y `Control Versiones`. No hace falta que GeneradorFIT V3 pueda leerla.
- Excel FIT, `User_TableInfo_DB` (`TableInfo_1…N`, una tabla por cada columna de fixture usada), `User_Config_Reference_FB` y `CheckTechTemplate_DB` con sus init SCL: igual que GeneradorFIT V3.0.1, salvo que el número de tecnología se escribe siempre con 2 cifras (`Tech02`, `Tech10`).

## Decisiones técnicas

- Solución .NET 10: `GstFitGenerator.Core` (modelo, cálculo, lectura y escritura) con tests, y `GstFitGenerator.App` (Avalonia).
- Los tests comprueban que, con los mismos datos, lo generado coincide con GeneradorFIT V3.0.1. Los datos de prueba son inventados.
- Lo que se guarda en el proyecto es el orden de las features; los pointers se calculan a partir de él. Para poder comparar, se guardan los offsets de la última generación.
- `./checks.sh` compila con los avisos como errores y pasa los tests.

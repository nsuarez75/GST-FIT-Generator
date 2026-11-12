# GST-FIT-Generator

Aplicación de escritorio para crear dentro de la propia app los datos del FIT de una pieza (proyectos, referencias, features, tipos de feature, tecnologías, locations y pantallas de trazabilidad) y, a partir de ellos, generar el Excel FIT para la plataforma I4 de GESTAMP y el código de PLC (TIA Portal) del estándar SICAR@GST. Sustituye a la plantilla Excel que antes se rellenaba a mano.

> Estado: versión 0.4.1 publicada en [Releases](https://github.com/nsuarez75/GST-FIT-Generator/releases), con ejecutable de Windows. Probada con la plantilla real y en TIA (fuentes de PLC y pantallas de techs).

## Uso

1. **Proyecto**: nombre, revisión, planta y línea. O bien **Importar plantilla** para crearlo a partir de una plantilla Excel de las de antes (o, si no hay plantilla, del Excel FIT de la instalación). La plantilla solo guarda el número de cada location, así que se crean como «Location N»: renómbralas en **Locations**. Para traer referencias de otra instalación a un proyecto ya abierto, **Más → Importar referencias de un Excel…**.
2. **Catálogo** (común a todos los proyectos): los tipos de feature. En **Tecnologías**, el nombre de cada tecnología (0 a 10) y la tecnología de cada tipo.
3. **Locations**, **Auxiliares**, **Pantallas** y **Control de versiones**. Dentro de **Pantallas** están **Screen Names** y **Pantallas de techs**: dónde empieza cada referencia en las pantallas de trazabilidad del HMI, los nombres cortos de Fixtures y Assets y una vista previa de cada pantalla.
4. **Referencias**: una tabla por referencia, agrupada por tecnología, con el pointer y el offset de cada feature. Se reordena arrastrando por el asa (o con Alt+↑/↓) dentro de su tecnología. «Eliminar» propone convertir la feature en reserva para no mover nada. Se pueden pegar filas de un Excel y duplicar la referencia.
5. **Tamaños**: las constantes `TechNN_Ref_MM` para TIA, listas para copiar.
6. **Generar** (F5): plantilla, FIT, `User_TableInfo_DB`, `User_Config_Reference_FB`, `CheckTechTemplate_DB` con sus SCL y las pantallas de techs en XML para importar en TIA, cada cosa en su subcarpeta. Antes de escribir enseña qué features han cambiado de posición desde la última generación.

El proyecto se guarda en un archivo `.fitproj` (Guardar como sirve para exportarlo) y el catálogo en `%APPDATA%\GST-FIT-Generator\catalogo.fitcat`. La interfaz está en castellano e inglés y tiene tema claro y oscuro.

## Documentación

- `docs/especificacion.md`: lo acordado en la entrevista inicial.
- `GLOSSARY.md`: vocabulario del proyecto. Léelo antes de nombrar cosas.
- `TODO.md`: pendientes.

## Estructura

- `src/GstFitGenerator.Core`: modelo del proyecto, cálculo de pointers y offsets, edición, tamaños, avisos, generación (plantilla, FIT y código de PLC) e importación de plantillas. Sin dependencias de interfaz.
- `src/GstFitGenerator.App`: la aplicación (Avalonia).
- `tests/GstFitGenerator.Tests`: tests del núcleo con datos inventados.
- `tests/GstFitGenerator.App.Tests`: tests de la interfaz sin pantalla. Con la variable `GST_SCREENSHOTS` apuntando a una carpeta, dejan ahí capturas de todas las pantallas.

Probado con la plantilla real de ejemplo: el FIT, `User_TableInfo_DB`, `User_Config_Reference_FB` y `CheckTechTemplate_DB` con sus SCL salen correctos.

## Datos de ejemplo

La carpeta `Datos Ejemplo/` (plantilla FIT real) está en `.gitignore`: son datos reales y solo sirven para conocer el formato. Los tests usan datos inventados.

## Comprobaciones

`./checks.sh` compila con los avisos como errores y pasa los tests. Sin `./checks.sh` en verde no hay commit ni PR.

## Licencia

El código se publica con licencia [MIT](LICENSE) © 2026 Ignacio Suarez. Las librerías de terceros que van dentro del
ejecutable tienen sus propias licencias: ver [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

La licencia no cubre el logotipo de Alomatek ni las marcas de terceros, que pertenecen a sus dueños.

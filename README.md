# GST-FIT-Generator

Aplicación de escritorio para crear dentro de la propia app los datos del FIT de una pieza (proyectos, referencias, features, tipos de feature, tecnologías, locations y pantallas de trazabilidad) y, a partir de ellos, generar lo mismo que GeneradorFIT: el Excel FIT para la plataforma I4 de GESTAMP y el código de PLC (TIA Portal) del estándar SICAR@GST. Sustituye a la plantilla Excel que antes se rellenaba a mano.

> Estado: núcleo hecho (cálculo, generación e importación de plantillas); falta la interfaz. Ver `TODO.md`.

## Documentación

- `docs/especificacion.md`: lo acordado en la entrevista inicial.
- `GLOSSARY.md`: vocabulario del proyecto. Léelo antes de nombrar cosas.
- `TODO.md`: pendientes.

## Estructura

- `src/GstFitGenerator.Core`: modelo del proyecto, cálculo de pointers y offsets, edición, tamaños, avisos, generación (plantilla, FIT y código de PLC) e importación de plantillas. Sin dependencias de interfaz.
- `tests/GstFitGenerator.Tests`: tests del núcleo con datos inventados.

Con la plantilla real de ejemplo, el núcleo genera el FIT, `User_TableInfo_DB`, `User_Config_Reference_FB` y `CheckTechTemplate_DB` con sus SCL exactamente iguales que GeneradorFIT V3.0.1.

## Datos de ejemplo

La carpeta `Datos Ejemplo/` (plantilla FIT real) está en `.gitignore`: son datos reales y solo sirven para conocer el formato. Los tests usan datos inventados.

## Comprobaciones

`./checks.sh` compila con los avisos como errores y pasa los tests. Sin `./checks.sh` en verde no hay commit ni PR.

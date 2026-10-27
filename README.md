# GST-FIT-Generator

Aplicación de escritorio para crear dentro de la propia app los datos del FIT de una pieza (proyectos, referencias, features, tipos de feature, tecnologías, locations y pantallas de trazabilidad) y, a partir de ellos, generar lo mismo que GeneradorFIT: el Excel FIT para la plataforma I4 de GESTAMP y el código de PLC (TIA Portal) del estándar SICAR@GST. Sustituye a la plantilla Excel que antes se rellenaba a mano.

> Estado: en definición. Las decisiones de producto se están cerrando en una entrevista; ver `TODO.md`.

## Documentación

- `GLOSSARY.md`: vocabulario del proyecto. Léelo antes de nombrar cosas.
- `TODO.md`: pendientes.

## Datos de ejemplo

La carpeta `Datos Ejemplo/` (plantilla FIT real) está en `.gitignore`: son datos reales y solo sirven para conocer el formato. Los tests usan datos inventados.

## Comprobaciones

`./checks.sh` compila y pasa los tests (se crea junto con la solución). Sin `./checks.sh` en verde no hay commit ni PR.

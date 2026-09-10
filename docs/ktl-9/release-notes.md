# Nota de versión KTL-9

La carga de un CV ahora transmite y conserva el archivo real en almacenamiento privado. Un
mensaje de carga correcta significa **«archivo aceptado y en análisis»**. El documento aparece
como `En análisis` y no puede descargarse hasta que el análisis de seguridad termina con un
resultado limpio. Los archivos rechazados permanecen visibles como no disponibles con una
explicación no técnica.

También se amplía la validación de cortesía de la interfaz a 20 MiB y diez clases permitidas,
se separan los permisos de carga y descarga, y eliminar el CV principal ya no promociona otro
automáticamente.

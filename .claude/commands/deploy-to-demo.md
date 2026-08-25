---
name: 'Deploy to Demo'
description: Promover la rama actual a través de main → demo con rollback automático en conflictos
category: Workflow
tags: [deploy, git, workflow]
---

Ejecuta el pipeline de despliegue seguro: promueve la rama actual a través de `main` y `demo`, hace push de ambas, y vuelve a la rama original.

**Uso**: `/deploy-to-demo` (sin argumentos, debe ejecutarse desde una rama de trabajo)

**Pasos**

1. Verificar que el usuario está en una rama de trabajo (no `main` ni `demo`). Si no, informar y parar.

2. Ejecutar el script de despliegue:

   ```bash
   bash scripts/deploy-to-demo.sh
   ```

3. Mostrar la salida completa del script al usuario.

4. Si el script termina con éxito (exit code 0), confirmar:
   - Que `main` y `demo` han sido actualizados
   - Que el push al remoto `hms` se ha completado
   - La rama en la que está el usuario ahora

5. Si el script termina con error (exit code != 0), explicar:
   - El paso donde falló (las líneas `[ERROR]` indican el motivo)
   - Si hubo rollback automático (indicado en la salida `[OK] main revertido...`)
   - Qué debe hacer el usuario para continuar (resolver conflictos, hacer commit, etc.)

**Requisitos previos (el script los valida automáticamente):**

- Estar en una rama de trabajo (no `main` ni `demo`)
- No tener cambios en archivos rastreados sin commitear
- Tener al menos un commit nuevo respecto a `main`

**No ejecutar este comando si:**

- Hay trabajo en progreso sin commitear
- El usuario está en `main` o `demo`

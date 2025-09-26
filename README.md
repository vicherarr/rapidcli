# RapidCLI

RapidCLI es un asistente de inteligencia artificial para terminal construido con .NET 8 y una arquitectura limpia. Proporciona una experiencia de conversación productiva y extensible desde la línea de comandos, con soporte inicial para el proveedor Chutes.ai.

## Características principales

- Arquitectura en capas (Domain, Application, Infrastructure, CLI) pensada para la extensibilidad.
- Integración con el endpoint de chat de Chutes.ai mediante Refit.
- Interfaz rica en la terminal con Spectre.Console, incluyendo tablas, reglas y colores ANSI.
- Historial de conversación multi-turno con posibilidad de guardado y carga de sesiones.
- Configuración dinámica de parámetros del modelo (temperature, top_p, max_tokens, etc.).
- Comandos rápidos (`/help`, `/config`, `/reset`, `/save`, `/load`, `/sessions`, `/exit`).
- Streaming de respuestas cuando el modelo lo soporta.
- Modo agente permanente con acceso controlado al sistema de archivos para analizar y modificar código.

## Requisitos previos

- .NET SDK 8.0
- Token válido de Chutes.ai disponible como variable de entorno (`CHUTES_API_KEY`)

## Configuración

1. Clona el repositorio y restaura los paquetes:

   ```bash
   dotnet restore
   ```

2. Define el token del proveedor como variable de entorno antes de iniciar el CLI:

   ```bash
   export CHUTES_API_KEY="tu_token"
   ```

3. Opcionalmente puedes ajustar parámetros por defecto en `appsettings.json` o mediante `~/.rapidcli/config.json` generado tras usar `/config set`. Si prefieres no usar variables de entorno globales, también puedes definir `ChutesAi:ApiToken` en un archivo de configuración adicional.

## Uso

Ejecuta el asistente con:

```bash
dotnet run --project src/CLI --
```

Durante la sesión interactiva puedes:

- Describir una tarea y dejar que el agente actúe sobre el repositorio (si está habilitado).
- Usar `/help` para ver el listado de comandos disponibles.
- Personalizar la configuración con `/config set temperature 0.5` (disponible para `model`, `temperature`, `top_p`, `max_tokens`, `frequency_penalty`, `presence_penalty` y `stream`).
- Guardar la sesión actual con `/save nombre` y volver a cargarla con `/load nombre`.
- Ajustar parámetros del agente con `/config set agent.enabled true` y otras propiedades descritas abajo.

### Configuración del agente

Los parámetros del agente pueden ajustarse con `/config set` utilizando claves anidadas:

- `agent.enabled`: habilita o deshabilita el modo agente.
- `agent.model`: modelo dedicado para tareas del agente (por defecto usa el modelo de chat).
- `agent.max_iterations`: número máximo de pasos de razonamiento.
- `agent.allow_file_writes`: permite o bloquea modificaciones en disco.
- `agent.working_directory`: ruta (relativa o absoluta) en la que el agente puede operar.

> **Nota:** el modo agente requiere un modelo con soporte para llamadas a herramientas (tool calls) compatibles con el esquema de OpenAI/Chutes.ai.

## Estructura del proyecto

```
appsettings.json
src/
  Domain/
  Application/
  Infrastructure/
  CLI/
```

Cada capa encapsula responsabilidades específicas, manteniendo la adherencia a los principios de Clean Architecture.

## Pruebas y calidad

Actualmente el proyecto no incluye pruebas automatizadas. Se recomienda ejecutar `dotnet build` para asegurar que el código compila correctamente antes de cualquier cambio.

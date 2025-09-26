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

## Requisitos previos

- .NET SDK 8.0
- Token válido de Chutes.ai (`CHUTES_API_TOKEN`)

## Configuración

1. Clona el repositorio y restaura los paquetes:

   ```bash
   dotnet restore
   ```

2. Define el token del proveedor como variable de entorno antes de iniciar el CLI:

   ```bash
   export RAPIDCLI_CHUTESAI__APITOKEN="tu_token"
   ```

3. Opcionalmente puedes ajustar parámetros por defecto en `appsettings.json` o mediante `~/.rapidcli/config.json` generado tras usar `/config set`.

## Uso

Ejecuta el asistente con:

```bash
dotnet run --project src/CLI --
```

Durante la sesión interactiva puedes:

- Enviar mensajes libremente para conversar con el modelo.
- Usar `/help` para ver el listado de comandos disponibles.
- Personalizar la configuración con `/config set temperature 0.5` (disponible para `model`, `temperature`, `top_p`, `max_tokens`, `frequency_penalty`, `presence_penalty` y `stream`).
- Guardar la sesión actual con `/save nombre` y volver a cargarla con `/load nombre`.

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

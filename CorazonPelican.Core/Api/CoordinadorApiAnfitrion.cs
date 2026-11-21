using System;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Sensorio;

namespace CorazonPelican.Core.Api
{
    /// <summary>
    /// Conecta <see cref="ApiMotor"/> con los motores por personaje del anfitrión:
    /// instantáneas de memoria, escena actual y aplicación atómica de respuestas (ADS §5, CP-213).
    /// </summary>
    public static class CoordinadorApiAnfitrion
    {
        public static void Conectar(
            ApiMotor api,
            Func<string, MotorHabitante?> obtenerMotor,
            Func<string, EstadoPersonaje?> obtenerEstado,
            bool enHiloPrincipal = true,
            Func<string, Observacion?>? observarEnVivo = null)
        {
            if (api is null)
            {
                throw new ArgumentNullException(nameof(api));
            }

            if (obtenerMotor is null)
            {
                throw new ArgumentNullException(nameof(obtenerMotor));
            }

            if (obtenerEstado is null)
            {
                throw new ArgumentNullException(nameof(obtenerEstado));
            }

            api.RegistrarProveedorMemoria(
                personaje =>
                {
                    var motor = obtenerMotor(personaje);
                    if (motor is not null)
                    {
                        motor.Epoca = api.EpocaVigente ?? motor.Epoca;
                        return motor.ConstruirInstantaneaMemoria();
                    }

                    var estado = obtenerEstado(personaje);
                    return estado is null
                        ? null
                        : Consolidacion.ProyectarInstantaneaMemoria(estado, personaje, api.EpocaVigente ?? string.Empty);
                },
                enHiloPrincipal);
            api.RegistrarProveedorEscena(
                personaje =>
                {
                    var motor = obtenerMotor(personaje);
                    if (motor is null)
                    {
                        return null;
                    }

                    motor.Epoca = api.EpocaVigente ?? motor.Epoca;
                    var obs = observarEnVivo?.Invoke(personaje);
                    return motor.ConstruirEscenaActualApi(obs);
                },
                enHiloPrincipal);
            api.RegistrarAplicadorPorDefecto((pulso, respuesta) =>
            {
                var motor = obtenerMotor(pulso.Personaje);
                if (motor is null)
                {
                    return ApiMotor.TieneEfectos(respuesta)
                        ? Resultado.Rechazar(new MotivoRechazo(
                            "sin_aplicador",
                            "respuesta",
                            $"no hay ningún motor activo para aplicar la respuesta de «{pulso.Personaje}»"))
                        : new Resultado { Estado = Resultado.Aceptada };
                }

                return motor.AplicarRespuesta(pulso, respuesta);
            });
        }

        public static void Desconectar(ApiMotor api)
        {
            if (api is null)
            {
                throw new ArgumentNullException(nameof(api));
            }

            api.RegistrarProveedorEscena(null);
            api.RegistrarAplicadorPorDefecto(null);
        }
    }
}

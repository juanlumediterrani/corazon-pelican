using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CorazonPelican.Core.Api;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Tests.Contrato;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Memoria;

public class ReflexionDiarioTests
{
    [Fact]
    public async Task Recorrido_completo_desde_diario_y_pulso_hasta_api_motor_y_estado_persistido_conserva_origen_diario()
    {
        var partida = new EstadoPartida();
        var estado = partida.ObtenerOIniciar("habitante");

        var bufer = new Bufer();
        bufer.RegistrarPercepcion(4, 1500, Banda.Mencion, 0.58, "La lluvia golpeaba el tejado mientras trabajabas.", "clima:lluvia");
        bufer.RegistrarAccion(4, 1900, "Al caer la tarde guardaste el cuaderno.", 0.74);
        var episodios = Consolidacion.RegistrarDesdeBufer(estado, bufer);
        Assert.Equal(new[] { "ep-1", "ep-2" }, episodios.Select(e => e.Id));

        var pulso = new Pulso
        {
            Id = 52,
            Epoca = "primavera-ep",
            Personaje = "habitante",
            Motivo = Motivos.Hito,
            TiempoJuego = new TiempoJuego { Dia = 4, Hora = 2200 },
            Hito = new HitoPulso { Id = "acostarse", Origen = "rutina", Forma = "cadena" },
            Escena = "Cierras el cuaderno al acabar el día.",
        };
        Consolidacion.AplicarEpisodiosAlPulso(pulso, estado, diaJuego: 4);
        Assert.Equal(new[] { "ep-1", "ep-2" }, pulso.EpisodiosDiario!.Keys);

        var motor = new ApiMotor();
        motor.IniciarEpoca("primavera-ep", new[] { "habitante" });
        motor.RegistrarProveedorEstado(p => partida.ObtenerSiExiste(p));
        motor.RegistrarAplicador("habitante", (p, r) =>
        {
            var aplicacion = Consolidacion.AplicarMemoria(estado, p, r);
            return aplicacion.Aceptada
                ? new Resultado { Estado = Resultado.Aceptada }
                : Resultado.Rechazar(aplicacion.MotivoRechazo!);
        });

        motor.EmitirPulso(pulso);

        var respuesta = new Respuesta
        {
            Pensamiento = "Pude quedarme un rato más con el dibujo.",
            Epoca = "primavera-ep",
            BasadaEn = 52,
            Memoria = new MemoriaPropuesta
            {
                Reflexiones = new List<ReflexionPropuesta>
                {
                    new()
                    {
                        Texto = "Me vino bien aquella tarde tranquila bajo la lluvia.",
                        OrigenDiario = new List<string> { "ep-1", "ep-2" },
                    },
                },
            },
        };

        var tareaRespuesta = motor.ProcesarRespuestaAsync(respuesta);
        motor.DrenarCola();
        var resultado = await tareaRespuesta;

        Assert.Equal(Resultado.Aceptada, resultado.Estado);
        var recuerdo = Assert.Single(estado.Recuerdos);
        Assert.Equal("rec-1", recuerdo.Id);
        Assert.Equal("Me vino bien aquella tarde tranquila bajo la lluvia.", recuerdo.Texto);
        Assert.Equal(new[] { "ep-1", "ep-2" }, recuerdo.OrigenDiario);
        Assert.Equal(0.74, recuerdo.Importancia, precision: 3);

        // La instantánea de GET /api/v1/memoria/{personaje} expone episodios y recuerdos con su origen_diario.
        var consultaMem = await motor.ObtenerMemoriaAsync("habitante");
        Assert.Equal(EstadoRecursoApi.Ok, consultaMem.Estado);
        Assert.Equal(2, consultaMem.Valor!.Diario.Count);
        var recuerdoApi = Assert.Single(consultaMem.Valor.Recuerdos);
        Assert.Equal(new[] { "ep-1", "ep-2" }, recuerdoApi.OrigenDiario);

        // Persistencia (System.Text.Json y Newtonsoft.Json como WriteSaveData) conserva origen_diario.
        var jsonCore = PersistenciaEstado.Serializar(partida);
        var restauradaCore = PersistenciaEstado.Deserializar(jsonCore);
        Assert.Empty(PersistenciaEstado.Validar(restauradaCore));
        var recuerdoRestaurado = Assert.Single(restauradaCore.ObtenerSiExiste("habitante")!.Recuerdos);
        Assert.Equal(new[] { "ep-1", "ep-2" }, recuerdoRestaurado.OrigenDiario);

        var jsonSmapi = JsonConvert.SerializeObject(partida);
        var restauradaSmapi = PersistenciaEstado.Deserializar(jsonSmapi);
        var recuerdoSmapi = Assert.Single(restauradaSmapi.ObtenerSiExiste("habitante")!.Recuerdos);
        Assert.Equal(new[] { "ep-1", "ep-2" }, recuerdoSmapi.OrigenDiario);
    }

    [Fact]
    public async Task Rechaza_origenes_ajenos_vacios_o_sin_origen_antes_de_guardar_en_estado()
    {
        var estado = new EstadoPersonaje();
        estado.AgregarEpisodio(4, 1500, "La lluvia golpeaba el tejado.", 0.6);

        var pulso = new Pulso
        {
            Id = 52,
            Epoca = "primavera-ep",
            Personaje = "habitante",
            Motivo = Motivos.Hito,
            TiempoJuego = new TiempoJuego { Dia = 4, Hora = 2200 },
            Hito = new HitoPulso { Id = "acostarse", Origen = "rutina", Forma = "cadena" },
            Escena = "Fin del día.",
        };
        Consolidacion.AplicarEpisodiosAlPulso(pulso, estado, diaJuego: 4);

        // 1. Cita un episodio ajeno que no figura en episodios_diario.
        var conAjeno = new Respuesta
        {
            Pensamiento = "Cito un episodio ajeno.",
            Epoca = "primavera-ep",
            BasadaEn = 52,
            Memoria = new MemoriaPropuesta
            {
                Reflexiones = new List<ReflexionPropuesta>
                {
                    new()
                    {
                        Texto = "Recuerdo de otro personaje.",
                        OrigenDiario = new List<string> { "ep-1", "ep-999" },
                    },
                },
            },
        };
        var resAjeno = Consolidacion.AplicarMemoria(estado, pulso, conAjeno);
        Assert.False(resAjeno.Aceptada);
        Assert.Equal("episodio_desconocido", resAjeno.MotivoRechazo!.Codigo);
        Assert.Empty(estado.Recuerdos);

        // 2. Origen vacío (sin pasar por JSON Schema).
        var conOrigenVacio = new Respuesta
        {
            Pensamiento = "Sin cita.",
            Epoca = "primavera-ep",
            BasadaEn = 52,
            Memoria = new MemoriaPropuesta
            {
                Reflexiones = new List<ReflexionPropuesta>
                {
                    new() { Texto = "Sin respaldo.", OrigenDiario = new List<string>() },
                },
            },
        };
        var resVacio = Consolidacion.AplicarMemoria(estado, pulso, conOrigenVacio);
        Assert.False(resVacio.Aceptada);
        Assert.Equal("origen_ausente", resVacio.MotivoRechazo!.Codigo);
        Assert.Empty(estado.Recuerdos);

        // 3. Usa memoria.recordar en prosa cuando el pulso ofrece episodios_diario.
        var conRecordar = new Respuesta
        {
            Pensamiento = "En prosa suelta.",
            Epoca = "primavera-ep",
            BasadaEn = 52,
            Memoria = new MemoriaPropuesta { Recordar = new List<string> { "Sin origen." } },
        };
        var resRecordar = Consolidacion.AplicarMemoria(estado, pulso, conRecordar);
        Assert.False(resRecordar.Aceptada);
        Assert.Equal("origen_ausente", resRecordar.MotivoRechazo!.Codigo);
        Assert.Empty(estado.Recuerdos);

        // 4. También a través de ApiMotor se rechaza antes de invocar al aplicador.
        var motor = new ApiMotor();
        motor.IniciarEpoca("primavera-ep", new[] { "habitante" });
        var aplicadorInvocado = false;
        motor.RegistrarAplicador("habitante", (_, _) =>
        {
            aplicadorInvocado = true;
            return new Resultado { Estado = Resultado.Aceptada };
        });
        motor.EmitirPulso(pulso);

        var tareaApi = motor.ProcesarRespuestaAsync(conAjeno);
        motor.DrenarCola();
        var resApi = await tareaApi;

        Assert.Equal(Resultado.Rechazada, resApi.Estado);
        Assert.Equal("episodio_desconocido", resApi.Motivos!.Single().Codigo);
        Assert.False(aplicadorInvocado);
        Assert.Empty(estado.Recuerdos);
    }

    [Fact]
    public void Mantiene_compatibilidad_con_recuerdos_anteriores_en_prosa_sin_origen_diario()
    {
        // A) Respuesta con memoria.recordar cuando el pulso NO ofrecía episodios_diario (formato anterior).
        var estado = new EstadoPersonaje();
        var pulsoAntiguo = new Pulso
        {
            Id = 10,
            Epoca = "otono",
            Personaje = "habitante",
            Motivo = Motivos.Hito,
            TiempoJuego = new TiempoJuego { Dia = 12, Hora = 2200 },
            Hito = new HitoPulso { Id = "acostarse", Origen = "rutina", Forma = "cadena" },
            Escena = "Fin de la jornada.",
            DiarioDia = new List<string> { "Una tarde tranquila en el estudio." },
            EpisodiosDiario = null,
        };
        var respuestaProsa = new Respuesta
        {
            Pensamiento = "Guardo esa sensación.",
            Epoca = "otono",
            BasadaEn = 10,
            Memoria = new MemoriaPropuesta
            {
                Recordar = new List<string> { "Trabajar en calma me dio claridad." },
            },
        };

        var res = Consolidacion.AplicarMemoria(estado, pulsoAntiguo, respuestaProsa);
        Assert.True(res.Aceptada);
        var guardado = Assert.Single(estado.Recuerdos);
        Assert.Equal("Trabajar en calma me dio claridad.", guardado.Texto);
        Assert.Empty(guardado.OrigenDiario);

        // B) Estado persistido v1 con recuerdo en prosa sin origen_diario ni id explícito migra y se proyecta bien.
        const string jsonHeredado = """
        {
          "version_esquema": 1,
          "personajes": {
            "habitante": {
              "version_esquema": 1,
              "recuerdos": [
                {
                  "texto": "Recuerdo antiguo guardado solo en prosa.",
                  "importancia": 0.6
                }
              ]
            }
          }
        }
        """;

        var migrado = PersistenciaEstado.Deserializar(jsonHeredado);
        Assert.Empty(PersistenciaEstado.Validar(migrado));
        var personaje = migrado.ObtenerSiExiste("habitante")!;
        var recuerdoMigrado = Assert.Single(personaje.Recuerdos);
        Assert.Equal("rec-1", recuerdoMigrado.Id);
        Assert.Equal("Recuerdo antiguo guardado solo en prosa.", recuerdoMigrado.Texto);
        Assert.NotNull(recuerdoMigrado.OrigenDiario);
        Assert.Empty(recuerdoMigrado.OrigenDiario);

        var instantanea = Consolidacion.ProyectarInstantaneaMemoria(personaje, "habitante", "otono");
        Assert.Equal("Recuerdo antiguo guardado solo en prosa.", Assert.Single(instantanea.Recuerdos).Texto);
        Assert.Equal(new[] { "Recuerdo antiguo guardado solo en prosa." }, Consolidacion.ProyectarRecuerdosParaPulso(personaje));

        // La instantánea proyectada de respuesta con ejemplo oficial sigue cumpliendo el esquema.
        var pulsoEjemplo = System.Text.Json.JsonSerializer.Deserialize<Pulso>(
            File.ReadAllText(Path.Combine(Esquemas.Ejemplos("validos"), "pulso--diario-con-origen.json")))!;
        var respEjemplo = System.Text.Json.JsonSerializer.Deserialize<Respuesta>(
            File.ReadAllText(Path.Combine(Esquemas.Ejemplos("validos"), "respuesta--reflexion-con-origen.json")))!;
        var nodoResp = System.Text.Json.JsonSerializer.SerializeToNode(
            respEjemplo,
            new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        Assert.True(Esquemas.Evaluar("respuesta", nodoResp).IsValid);

        var estadoEjemplo = new EstadoPersonaje();
        var resEjemplo = Consolidacion.AplicarMemoria(estadoEjemplo, pulsoEjemplo, respEjemplo);
        Assert.True(resEjemplo.Aceptada);
        Assert.Equal(new[] { "ep-8", "ep-9" }, Assert.Single(estadoEjemplo.Recuerdos).OrigenDiario);
    }
}

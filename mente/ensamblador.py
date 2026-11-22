"""Ensamblado determinista de prompts para la mente (ADS §6, P-11)."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any

REGLAS_SISTEMA = """\
Habitas el valle desde dentro de la escena que recibes. Piensa como el personaje antes de comprometerte con ninguna acción.

Reglas de respuesta (contrato P-11):
- Responde exclusivamente con un objeto JSON válido que cumpla el esquema proporcionado.
- El campo `pensamiento` va siempre primero: es prosa privada en primera persona; el juego nunca lo ejecuta.
- Decide en los campos efectivos (`intencion.plan`, `intencion.cambios_rutina`, `actos`, `agenda`) usando únicamente acciones, lugares, referencias (`ref`), hitos y obras que figuren en el pulso actual. Nunca inventes un lugar ni un identificador que no esté en el apéndice o en tus recuerdos.
- Si lo que quieres no tiene identificador en el apéndice, elige entre las opciones disponibles o renuncia en tus propias palabras.
- Omite los campos opcionales cuando no correspondan en lugar de enviarlos como `null`.
- En pulsos con `motivo=interlocucion`, incluye siempre `frase` (lo que dices en voz alta al interlocutor, breve y natural en español).
- Si propones `actos` con `cuando` (`encargo`, `cita`), el día y hora deben ser estrictamente posteriores al momento actual del pulso.
- Si la respuesta anterior fue rechazada, reformula atendiendo a la prosa de cada motivo."""


@dataclass(frozen=True)
class PromptEnsamblado:
    """Prompt separado en prefijo estable (caché del proveedor) y bloque dinámico del pulso."""

    sistema: str
    usuario: str


def cargar_voz(ruta: str | Path) -> str:
    """Lee el fichero markdown de voz de un personaje."""
    return Path(ruta).read_text(encoding="utf-8")


def construir_prefijo_sistema(voz: str) -> str:
    """Construye el prefijo de sistema estable (reglas + voz), idéntico entre pulsos."""
    voz_limpia = voz.replace("\r\n", "\n").strip()
    return f"{REGLAS_SISTEMA}\n\n## Voz del personaje\n\n{voz_limpia}"


def _formatear_hora(hora: int) -> str:
    return f"{hora // 100:02d}:{hora % 100:02d}"


def _formatear_tiempo_juego(tiempo: dict[str, Any] | None) -> str:
    if not tiempo:
        return "desconocido"
    return f"día {tiempo.get('dia', '?')}, {_formatear_hora(int(tiempo.get('hora', 0)))}"


def _formatear_hechos(hechos: dict[str, Any]) -> list[str]:
    lineas: list[str] = []
    if "energia" in hechos:
        lineas.append(f"- energía: {hechos['energia']}")
    cerca = hechos.get("cerca") or []
    if cerca:
        for ent in cerca:
            partes = [f"{ent.get('ref')} ({ent.get('nombre')})"]
            if "corazones" in ent:
                partes.append(f"{ent['corazones']} corazones")
            if "distancia" in ent:
                partes.append(f"a {ent['distancia']} casillas")
            lineas.append("- cerca: " + ", ".join(partes))
    else:
        lineas.append("- cerca: nadie")
    for clave in sorted(hechos):
        if clave in {"energia", "cerca"}:
            continue
        lineas.append(f"- {clave}: {hechos[clave]}")
    return lineas


def _formatear_recuerdos(recuerdos: list[Any]) -> list[str]:
    lineas: list[str] = []
    for item in recuerdos:
        if isinstance(item, dict):
            texto = item.get("texto") or item.get("prosa") or str(item)
            origen = item.get("origen_diario")
            if isinstance(origen, list) and origen:
                etiqueta = ", ".join(f"`{ep}`" for ep in origen if isinstance(ep, str) and ep)
                if etiqueta:
                    lineas.append(f"- {texto} (origen: {etiqueta})")
                    continue
            if item.get("fuente") == "diario" and isinstance(item.get("id"), str) and item["id"]:
                lineas.append(f"- `{item['id']}` — {texto}")
                continue
            lineas.append(f"- {texto}")
        else:
            lineas.append(f"- {item}")
    return lineas


def _formatear_motivos_rechazo(motivos_rechazo: list[Any]) -> list[str]:
    lineas: list[str] = []
    for motivo in motivos_rechazo:
        if isinstance(motivo, dict):
            prosa = motivo.get("prosa", "")
            codigo = motivo.get("codigo")
            campo = motivo.get("campo")
            if codigo or campo:
                etiqueta = " · ".join(p for p in (codigo, campo) if p)
                lineas.append(f"- [{etiqueta}] {prosa}")
            else:
                lineas.append(f"- {prosa}")
        else:
            lineas.append(f"- {motivo}")
    return lineas


def ensamblar_prompt(
    voz: str,
    pulso: dict[str, Any],
    recuerdos: list[Any] | None = None,
    motivos_rechazo: list[Any] | None = None,
    *,
    pasos_cadena: list[dict[str, Any]] | None = None,
    instruccion_paso: str | None = None,
) -> PromptEnsamblado:
    """Ensambla el prompt de la mente manteniendo el prefijo `sistema` invariante entre pulsos."""
    sistema = construir_prefijo_sistema(voz)

    secciones: list[str] = []

    encabezado = [
        f"Pulso #{pulso.get('id')} · motivo={pulso.get('motivo')} · {_formatear_tiempo_juego(pulso.get('tiempo_juego'))}"
    ]
    if pulso.get("anticipado"):
        encabezado.append("Modo: pensamiento anticipado")
    if "hito" in pulso and pulso["hito"]:
        h = pulso["hito"]
        encabezado.append(f"Hito: {h.get('id')} (origen={h.get('origen')}, forma={h.get('forma')})")
    secciones.append("\n".join(encabezado))

    secciones.append(f"## Escena\n{pulso.get('escena', '')}")

    if pulso.get("interlocutor"):
        inter = pulso["interlocutor"]
        bloque_inter = [f"De `{inter.get('ref')}`: \"{inter.get('texto', '')}\""]
        if "caduca_en_ms" in pulso:
            bloque_inter.append(f"(Caduca en {pulso['caduca_en_ms']} ms)")
        secciones.append("## Interlocutor\n" + " ".join(bloque_inter))

    hechos = pulso.get("hechos") or {}
    secciones.append("## Hechos\n" + "\n".join(_formatear_hechos(hechos)))

    lugares = pulso.get("lugares") or []
    if lugares:
        lineas_lug = [f"- `{l['id']}` — {l['prosa']}" for l in lugares]
    else:
        lineas_lug = ["- (ninguno)"]
    secciones.append("## Lugares\n" + "\n".join(lineas_lug))

    acciones = pulso.get("acciones_disponibles") or []
    secciones.append("## Acciones disponibles\n" + (", ".join(acciones) if acciones else "(ninguna)"))

    obras = pulso.get("obras") or []
    if obras:
        lineas_obras = [f"- `{o['id']}` [{o['estado']}] — {o['prosa']}" for o in obras]
        secciones.append("## Obras\n" + "\n".join(lineas_obras))

    bufer = pulso.get("bufer") or []
    if bufer:
        lineas_bufer = [f"- [{_formatear_hora(int(b['hora']))}] {b['prosa']}" for b in bufer]
    else:
        lineas_bufer = ["- (sin entradas recientes)"]
    secciones.append("## Búfer\n" + "\n".join(lineas_bufer))

    huella = pulso.get("huella") or {}
    lineas_huella = [
        f"Desde el último pensamiento: {huella.get('desde_ultimo_pensamiento_min_juego', 0)} min de juego."
    ]
    if huella.get("prosa"):
        lineas_huella.append(huella["prosa"])
    for ent in huella.get("entidades") or []:
        lineas_huella.append(f"- `{ent['ref']}` (atención {ent['atencion']})")
    secciones.append("## Huella de atención\n" + "\n".join(lineas_huella))

    if pulso.get("intencion_activa"):
        ia = pulso["intencion_activa"]
        secciones.append(f"## Intención activa\n- `{ia.get('id')}`: {ia.get('objetivo')}")

    agenda = pulso.get("agenda") or []
    if agenda:
        lineas_ag = [f"- `{a['hito']}` [{a['estado']}] — {a['prosa']}" for a in agenda]
    else:
        lineas_ag = ["- (vacía)"]
    secciones.append("## Agenda\n" + "\n".join(lineas_ag))

    compromisos = pulso.get("compromisos_proximos") or []
    if compromisos:
        lineas_comp = [
            f"- `{c['id']}` con `{c['con']}` ({_formatear_tiempo_juego(c.get('plazo_juego'))}): {c['prosa']}"
            for c in compromisos
        ]
        secciones.append("## Compromisos próximos\n" + "\n".join(lineas_comp))

    episodios_diario = pulso.get("episodios_diario")
    if isinstance(episodios_diario, dict) and episodios_diario:
        lineas_ep = [f"- `{ep_id}` — {prosa}" for ep_id, prosa in episodios_diario.items()]
        secciones.append("## Episodios del diario\n" + "\n".join(lineas_ep))
    elif pulso.get("diario_dia"):
        lineas_dd = [f"- {prosa}" for prosa in pulso["diario_dia"]]
        secciones.append("## Diario del día\n" + "\n".join(lineas_dd))

    recuerdos_activos = recuerdos if recuerdos is not None else pulso.get("recuerdos")
    if recuerdos_activos:
        secciones.append("## Recuerdos\n" + "\n".join(_formatear_recuerdos(recuerdos_activos)))

    humor = pulso.get("humor")
    if isinstance(humor, dict) and humor.get("resumen"):
        lineas_hum = [
            f"{humor['resumen']} (intensidad {float(humor.get('intensidad', 0.0)):.2f})",
        ]
        origen_hum = humor.get("origen_diario") or []
        if origen_hum:
            etiquetas = ", ".join(f"`{ep}`" for ep in origen_hum if isinstance(ep, str) and ep)
            if etiquetas:
                lineas_hum.append(f"Origen: {etiquetas}")
        secciones.append("## Humor vigente\n" + "\n".join(lineas_hum))

    if motivos_rechazo:
        secciones.append(
            "## Rechazo anterior — reformula tu respuesta\n"
            + "\n".join(_formatear_motivos_rechazo(motivos_rechazo))
        )

    if pasos_cadena:
        lineas_pasos = [
            f"{i}. **{paso.get('paso', f'paso_{i}')}**: {paso.get('pensamiento', '')}"
            for i, paso in enumerate(pasos_cadena, start=1)
        ]
        secciones.append("## Pensamiento previo de la cadena\n" + "\n".join(lineas_pasos))

    if instruccion_paso:
        secciones.append(f"## Paso actual de la cadena\n{instruccion_paso}")

    return PromptEnsamblado(sistema=sistema, usuario="\n\n".join(secciones))

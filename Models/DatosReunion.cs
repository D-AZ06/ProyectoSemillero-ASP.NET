using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ProyectoSemillero_ASP.NET.Models
{
    [BsonIgnoreExtraElements]
    public class DatosReunion
    {
        [BsonElement("idReunion")]
        public int IdReunion { get; set; }

        [BsonElement("idSemillero")]
        public int IdSemillero { get; set; }

        [BsonElement("fechaReunion")]
        public string FechaReunion { get; set; }

        [BsonElement("horaInicio")]
        public string HoraInicio { get; set; }

        [BsonElement("horaFin")]
        public string HoraFin { get; set; }

        [BsonElement("motivoReunion")]
        public string MotivoReunion { get; set; }

        [BsonElement("lugarReunion")]
        public string LugarReunion { get; set; }

        [BsonElement("idLider")]
        public int IdLider { get; set; }

        [BsonElement("investigadoresConvocados")]
        public List<InvestigadorConvocado> InvestigadoresConvocados { get; set; } = new List<InvestigadorConvocado>();

        [BsonElement("estadoReunion")]
        public string EstadoReunion { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class InvestigadorConvocado
    {
        // Atrapamos cualquier formato viejo o nuevo que tenga tu BD
        [BsonElement("idInvestigador")]
        public int? IdInvestigador { get; set; }

        [BsonElement("idUsuario")]
        public int? IdUsuario { get; set; }

        // Propiedad inteligente: Toma el que exista.
        public int IdReal => IdInvestigador ?? IdUsuario ?? 0;

        [BsonElement("nombre")]
        public string Nombre { get; set; }

        [BsonElement("asistencia")]
        public string EstadoAsistencia { get; set; }
    }
}
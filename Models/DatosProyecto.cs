using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ProyectoSemillero_ASP.NET.Models
{
    public class DatosProyecto
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } // ID interno de MongoDB (opcional, pero recomendado)

        [BsonElement("idProyecto")]
        public int IdProyecto { get; set; }

        [BsonElement("idSemillero")]
        public int IdSemillero { get; set; }

        [BsonElement("tituloProyecto")]
        public string TituloProyecto { get; set; }

        [BsonElement("objetivoProyecto")]
        public string ObjetivoProyecto { get; set; }

        [BsonElement("descripcionProyecto")]
        public string DescripcionProyecto { get; set; }

        [BsonElement("fechaInicioProyecto")]
        public string FechaInicioProyecto { get; set; }

        [BsonElement("fechaFinProyecto")]
        public string FechaFinProyecto { get; set; }

        // Aquí se mapea el array de actividades de MongoDB
        [BsonElement("actividades")]
        public List<Actividad> Actividades { get; set; } = new List<Actividad>();
    }

    public class Actividad
    {
        [BsonElement("idActividad")]
        public int IdActividad { get; set; }

        [BsonElement("nombreActividad")]
        public string NombreActividad { get; set; }

        [BsonElement("duracionActividad")]
        public string DuracionActividad { get; set; }

        [BsonElement("fechaEntregaActividad")]
        public string FechaEntregaActividad { get; set; }

        // Aquí se mapea el array de fases dentro de cada actividad
        [BsonElement("fases")]
        public List<Fase> Fases { get; set; } = new List<Fase>();
    }

    public class Fase
    {
        [BsonElement("idFase")]
        public int IdFase { get; set; }

        [BsonElement("nombreFase")]
        public string NombreFase { get; set; }

        [BsonElement("duracionFase")]
        public string DuracionFase { get; set; }
    }
}
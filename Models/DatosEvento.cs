using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace ProyectoSemillero_ASP.NET.Models
{
    [BsonIgnoreExtraElements]
    public class DatosEvento
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("idEvento")]
        public int IdEvento { get; set; }

        [BsonElement("nombreEvento")]
        public string NombreEvento { get; set; }

        [BsonElement("tipoEvento")]
        public string TipoEvento { get; set; }

        [BsonElement("fechaEvento")]
        public string FechaEvento { get; set; }

        // =====================================
        // NUEVOS CAMPOS: Tiempos y Detalles
        // =====================================
        [BsonElement("horaInicio")]
        public string HoraInicio { get; set; }

        [BsonElement("horaFin")]
        public string HoraFin { get; set; }

        [BsonElement("estado")]
        public string Estado { get; set; }

        [BsonElement("modalidad")]
        public string Modalidad { get; set; }

        [BsonElement("enlaceReunion")]
        public string EnlaceReunion { get; set; }

        [BsonElement("descripcion")]
        public string Descripcion { get; set; }

        [BsonElement("capacidadMaxima")]
        public int CapacidadMaxima { get; set; }

        [BsonElement("requiereInscripcion")]
        public bool RequiereInscripcion { get; set; }

        [BsonElement("agenda")]
        public List<ItemAgenda> Agenda { get; set; } = new List<ItemAgenda>();
        // =====================================

        [BsonElement("lugarEvento")]
        public string LugarEvento { get; set; }

        [BsonElement("organizadorEvento")]
        public string OrganizadorEvento { get; set; }

        [BsonElement("idSemillero")]
        public int IdSemillero { get; set; }

        [BsonElement("proyectosParticipantes")]
        public List<ProyectoParticipante> ProyectosParticipantes { get; set; } = new List<ProyectoParticipante>();

        [BsonElement("patrocinadores")]
        public List<DatosPatrocinador> Patrocinadores { get; set; } = new List<DatosPatrocinador>();
    }

    [BsonIgnoreExtraElements]
    public class ProyectoParticipante
    {
        [BsonElement("idProyecto")]
        public int IdProyecto { get; set; }

        [BsonElement("tituloProyecto")]
        public string TituloProyecto { get; set; }
    }

    // =====================================
    // NUEVA CLASE: Para gestionar la agenda
    // =====================================
    [BsonIgnoreExtraElements]
    public class ItemAgenda
    {
        [BsonElement("hora")]
        public string Hora { get; set; }

        [BsonElement("actividad")]
        public string Actividad { get; set; }

        [BsonElement("ponente")]
        public string Ponente { get; set; }
    }
}
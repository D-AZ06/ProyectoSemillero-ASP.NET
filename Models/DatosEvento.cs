using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
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

        [BsonElement("lugarEvento")]
        public string LugarEvento { get; set; }

        [BsonElement("organizadorEvento")]
        public string OrganizadorEvento { get; set; }

        [BsonElement("idSemillero")]
        public int IdSemillero { get; set; }

        [BsonElement("proyectosParticipantes")]
        public List<ProyectoParticipante> ProyectosParticipantes { get; set; } = new List<ProyectoParticipante>();

        // MODIFICADO: Ahora apunta a DatosPatrocinador, que es el modelo global unificado
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

    // NOTA: La clase Patrocinador que estaba aquí ya no es necesaria 
    // porque usamos directamente 'DatosPatrocinador' del archivo independiente.
}
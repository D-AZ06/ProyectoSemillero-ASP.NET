using System;
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ProyectoSemillero_ASP.NET.Models
{
    [BsonIgnoreExtraElements] // <-- Crucial: Esto evita que colapse si un documento de Mongo tiene campos de más o de menos
    public class DatosPatrocinador
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("idPatrocinador")]
        public int IdPatrocinador { get; set; }

        [BsonElement("nombrePatrocinador")]
        public string NombrePatrocinador { get; set; }

        [BsonElement("tipoPatrocinador")]
        public string TipoPatrocinador { get; set; }

        // En tu diagrama de Mongo es un 'long'. En C# debe ser long obligatoriamente.
        [BsonElement("telefonoPatrocinador")]
        public long TelefonoPatrocinador { get; set; }

        [BsonElement("correoPatrocinador")]
        public string CorreoPatrocinador { get; set; }
    }
}
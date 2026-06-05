using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ProyectoSemillero_ASP.NET.Models
{
    public class DatosSemillero
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } // ID interno de MongoDB (opcional, pero recomendado)

        [BsonElement("idSemillero")]
        public int IdSemillero { get; set; }

        [BsonElement("nombreSemillero")]
        public string nombreSemillero { get; set; }

        [BsonElement("lineaSemillero")]
        public string lineaSemillero { get; set; }

        [BsonElement("enfoqueSemillero")]
        public string enfoqueSemillero { get; set; }
    }
}
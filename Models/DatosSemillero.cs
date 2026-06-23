using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ProyectoSemillero_ASP.NET.Models
{
    [BsonIgnoreExtraElements]
    public class DatosSemillero
    {
        // Tu ID numérico limpio, sin las etiquetas BsonId ni ObjectId
        [BsonElement("idSemillero")]
        public int IdSemillero { get; set; }

        [BsonElement("nombreSemillero")]
        public string nombreSemillero { get; set; }

        [BsonElement("lineaSemillero")]
        public string LineaSemillero { get; set; }

        [BsonElement("enfoqueSemillero")]
        public string EnfoqueSemillero { get; set; }
    }
}
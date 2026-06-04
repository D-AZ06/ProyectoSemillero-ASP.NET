using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ProyectoSemillero_ASP.NET.Models
{
    // Ignora campos extra si en el futuro agregas más cosas a la BD y no están aquí
    [BsonIgnoreExtraElements]
    public class DatosUsuario
    {
        [BsonElement("idUsuario")]
        public int IdUsuario { get; set; }

        [BsonElement("nombreUsuario")]
        public string NombreUsuario { get; set; }

        [BsonElement("correoUsuario")]
        public string CorreoUsuario { get; set; }

        [BsonElement("contraseñaUsuario")]
        public string ContrasenaUsuario { get; set; }

        [BsonElement("rolUsuario")]
        public string RolUsuario { get; set; }

        [BsonElement("idSemillero")]
        public int? IdSemillero { get; set; }
    }
}
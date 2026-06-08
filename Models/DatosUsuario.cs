using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ProyectoSemillero_ASP.NET.Models
{
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

        // --- CAMPOS NUEVOS ---
        [BsonElement("edadUsuario")]
        public int EdadUsuario { get; set; }

        [BsonElement("telefonoUsuario")]
        public long TelefonoUsuario { get; set; }

        [BsonElement("idSemillero")]
        public int? IdSemillero { get; set; } // Puede ser null como en la imagen
    }
}
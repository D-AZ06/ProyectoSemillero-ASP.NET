using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using MongoDB.Driver;
using ProyectoSemillero_ASP.NET.Models; // Asegúrate de que coincida con tu espacio de nombres

namespace ProyectoSemillero_ASP.NET.Controllers
{
    public class PatrocinadoresController : Controller
    {
        // Instancias de MongoDB (Ajusta con tu clase Conexión si la manejas global)
        private readonly IMongoDatabase _baseDatos;

        public PatrocinadoresController()
        {
            // Reemplaza esto por tu cadena de conexión o invocación a tu clase Conexion
            var cliente = new MongoClient("mongodb://localhost:27017");
            _baseDatos = cliente.GetDatabase("SemitecDB"); // Nombre de tu base de datos
        }

        // GET: Patrocinadores
        // Renderiza la tabla con todo el directorio global
        public ActionResult Index()
        {
            try
            {
                var coleccion = _baseDatos.GetCollection<DatosPatrocinador>("Patrocinadores");
                var listaPatrocinadores = coleccion.Find(_ => true).ToList();

                return View(listaPatrocinadores);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "No se pudo cargar el directorio de patrocinadores: " + ex.Message;
                return View(new List<DatosPatrocinador>());
            }
        }


        public ActionResult Eliminar(string id)
        {
            // 1. Limpiamos espacios en blanco accidentales que puedan venir desde la vista
            if (id != null) id = id.Trim();

            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "El identificador del patrocinador llegó vacío al servidor.";
                return RedirectToAction("Index");
            }

            try
            {
                var coleccion = _baseDatos.GetCollection<DatosPatrocinador>("Patrocinadores");
                DeleteResult resultado = null;

                // INTENTO 1: Convertir la cadena de texto a un ObjectId nativo de MongoDB (El método más seguro)
                if (id.Length == 24 && MongoDB.Bson.ObjectId.TryParse(id, out MongoDB.Bson.ObjectId objectIdMongo))
                {
                    // Usamos un filtro genérico de BsonDocument para obligar al Driver a buscar por el ObjectId real
                    var filtroObjectId = Builders<DatosPatrocinador>.Filter.Eq("_id", objectIdMongo);
                    resultado = coleccion.DeleteOne(filtroObjectId);
                }

                // INTENTO 2: Si no borró, intentamos buscarlo como string plano por si se guardó diferente
                if (resultado == null || resultado.DeletedCount == 0)
                {
                    var filtroIdString = Builders<DatosPatrocinador>.Filter.Eq(p => p.Id, id);
                    resultado = coleccion.DeleteOne(filtroIdString);
                }

                // INTENTO 3: Si sigue sin borrar, usamos el ID numérico consecutivo (Ej: 800, 801)
                if (resultado.DeletedCount == 0 && int.TryParse(id, out int idNumerico))
                {
                    var filtroIdNumerico = Builders<DatosPatrocinador>.Filter.Eq(p => p.IdPatrocinador, idNumerico);
                    resultado = coleccion.DeleteOne(filtroIdNumerico);
                }

                // Veredicto final de la operación
                if (resultado != null && resultado.DeletedCount > 0)
                {
                    TempData["Exito"] = "Patrocinador eliminado correctamente de la base de datos.";
                }
                else
                {
                    TempData["Error"] = "No se pudo eliminar. El registro no existe o el ID no es compatible.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error crítico en el servidor al intentar eliminar: " + ex.Message;
            }

            // Redirecciona para refrescar la tabla del Index de inmediato
            return RedirectToAction("Index");
        }

        // GET: Patrocinadores/Agregar
        [HttpGet]
        public ActionResult Agregar()
        {
            var nuevoPatrocinador = new DatosPatrocinador();

            try
            {
                var coleccion = _baseDatos.GetCollection<DatosPatrocinador>("Patrocinadores");

                // Buscamos el patrocinador con el idPatrocinador más alto registrado en la BD
                var ultimoPatrocinador = coleccion.Find(Builders<DatosPatrocinador>.Filter.Empty)
                                                 .SortByDescending(p => p.IdPatrocinador)
                                                 .FirstOrDefault();

                // Si ya existen patrocinadores, sumamos 1 al último ID
                // SI NO EXISTE NINGUNO (colección vacía), obligamos a que empiece en 800
                if (ultimoPatrocinador != null)
                {
                    nuevoPatrocinador.IdPatrocinador = ultimoPatrocinador.IdPatrocinador + 1;
                }
                else
                {
                    nuevoPatrocinador.IdPatrocinador = 800; // Valor inicial personalizado
                }
            }
            catch (Exception)
            {
                // En caso de un fallo temporal de conexión a Mongo, dejamos el valor inicial
                nuevoPatrocinador.IdPatrocinador = 800;
            }

            // Retorna la vista con el ID ya calculado y bloqueado en pantalla
            return View(nuevoPatrocinador);
        }


        // POST: Patrocinadores/Agregar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Agregar(DatosPatrocinador nuevoPatrocinador)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var coleccion = _baseDatos.GetCollection<DatosPatrocinador>("Patrocinadores");

                    // Doble verificación antibucle o duplicados en el servidor
                    var existeDuplicado = coleccion.Find(p => p.IdPatrocinador == nuevoPatrocinador.IdPatrocinador).Any();

                    if (existeDuplicado)
                    {
                        var ultimo = coleccion.Find(Builders<DatosPatrocinador>.Filter.Empty)
                                              .SortByDescending(p => p.IdPatrocinador)
                                              .FirstOrDefault();

                        nuevoPatrocinador.IdPatrocinador = (ultimo != null) ? ultimo.IdPatrocinador + 1 : 800;
                    }

                    coleccion.InsertOne(nuevoPatrocinador);

                    TempData["Exito"] = "Patrocinador registrado correctamente con el ID " + nuevoPatrocinador.IdPatrocinador;
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al guardar el patrocinador: " + ex.Message;
            }

            return View(nuevoPatrocinador);
        }

        // GET: Patrocinadores/Modificar/5
        [HttpGet]
        public ActionResult Modificar(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return RedirectToAction("Index");
            }

            try
            {
                var coleccion = _baseDatos.GetCollection<DatosPatrocinador>("Patrocinadores");

                // Buscamos el patrocinador por su campo _id único de MongoDB
                var patrocinador = coleccion.Find(p => p.Id == id).FirstOrDefault();

                if (patrocinador == null)
                {
                    TempData["Error"] = "No se encontró el patrocinador especificado.";
                    return RedirectToAction("Index");
                }

                return View(patrocinador);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar los datos: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Patrocinadores/Modificar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Modificar(DatosPatrocinador patrocinadorActualizado)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var coleccion = _baseDatos.GetCollection<DatosPatrocinador>("Patrocinadores");

                    // Definimos el filtro usando el Id de MongoDB
                    var filtro = Builders<DatosPatrocinador>.Filter.Eq(p => p.Id, patrocinadorActualizado.Id);

                    // Reemplazamos el documento completo en la base de datos con los nuevos valores
                    var resultado = coleccion.ReplaceOne(filtro, patrocinadorActualizado);

                    if (resultado.ModifiedCount > 0)
                    {
                        TempData["Exito"] = "Patrocinador actualizado correctamente.";
                    }
                    else
                    {
                        TempData["Error"] = "No se realizaron cambios en el registro.";
                    }

                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al actualizar el patrocinador: " + ex.Message;
            }

            // Si el modelo no es válido o hay un error, regresa a la vista con los datos actuales
            return View(patrocinadorActualizado);
        }
    }
}
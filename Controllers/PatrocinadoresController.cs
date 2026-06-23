using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using MongoDB.Driver;
using ProyectoSemillero_ASP.NET.Models;

namespace ProyectoSemillero_ASP.NET.Controllers
{
    public class PatrocinadoresController : Controller
    {
        // Instancia global utilizando tu clase de conexión centralizada
        private Conexion conexionDB = new Conexion();

        // ==========================================
        // GET: Patrocinadores (Index con Filtros)
        // ==========================================
        public ActionResult Index(string tipoFiltro, string valorFiltro)
        {
            try
            {
                var coleccion = conexionDB.Database.GetCollection<DatosPatrocinador>("Patrocinadores");

                // Obtenemos TODOS los patrocinadores para alimentar el autocompletado del buscador en la vista
                ViewBag.TodosLosPatrocinadores = coleccion.Find(_ => true).ToList();

                // Iniciamos el constructor de filtros
                var builder = Builders<DatosPatrocinador>.Filter;
                FilterDefinition<DatosPatrocinador> filtroBusqueda = builder.Empty;

                if (!string.IsNullOrEmpty(tipoFiltro) && !string.IsNullOrEmpty(valorFiltro))
                {
                    valorFiltro = valorFiltro.Trim();

                    switch (tipoFiltro)
                    {
                        case "idPatrocinador":
                            if (int.TryParse(valorFiltro, out int idBusqueda))
                                filtroBusqueda = builder.Eq(p => p.IdPatrocinador, idBusqueda);
                            break;
                        case "nombre":
                            filtroBusqueda = builder.Regex(p => p.NombrePatrocinador, new MongoDB.Bson.BsonRegularExpression(valorFiltro, "i"));
                            break;
                        case "tipoSoporte":
                            filtroBusqueda = builder.Regex(p => p.TipoPatrocinador, new MongoDB.Bson.BsonRegularExpression(valorFiltro, "i"));
                            break;
                        case "correo":
                            filtroBusqueda = builder.Regex(p => p.CorreoPatrocinador, new MongoDB.Bson.BsonRegularExpression(valorFiltro, "i"));
                            break;
                    }
                }

                // Ejecutar la consulta en MongoDB
                var listaPatrocinadores = coleccion.Find(filtroBusqueda).ToList();

                // Guardar los datos en el ViewBag para mantener el estado del filtro en pantalla
                ViewBag.TipoFiltroActual = tipoFiltro;
                ViewBag.ValorFiltroActual = valorFiltro;

                return View(listaPatrocinadores);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "No se pudo cargar el directorio de patrocinadores: " + ex.Message;
                return View(new List<DatosPatrocinador>());
            }
        }

        // ==========================================
        // GET: Patrocinadores/Eliminar
        // ==========================================
        public ActionResult Eliminar(string id)
        {
            if (id != null) id = id.Trim();

            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "El identificador del patrocinador llegó vacío al servidor.";
                return RedirectToAction("Index");
            }

            try
            {
                var coleccion = conexionDB.Database.GetCollection<DatosPatrocinador>("Patrocinadores");
                DeleteResult resultado = null;

                if (id.Length == 24 && MongoDB.Bson.ObjectId.TryParse(id, out MongoDB.Bson.ObjectId objectIdMongo))
                {
                    var filtroObjectId = Builders<DatosPatrocinador>.Filter.Eq("_id", objectIdMongo);
                    resultado = coleccion.DeleteOne(filtroObjectId);
                }

                if (resultado == null || resultado.DeletedCount == 0)
                {
                    var filtroIdString = Builders<DatosPatrocinador>.Filter.Eq(p => p.Id, id);
                    resultado = coleccion.DeleteOne(filtroIdString);
                }

                if (resultado?.DeletedCount == 0 && int.TryParse(id, out int idNumerico))
                {
                    var filtroIdNumerico = Builders<DatosPatrocinador>.Filter.Eq(p => p.IdPatrocinador, idNumerico);
                    resultado = coleccion.DeleteOne(filtroIdNumerico);
                }

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

            return RedirectToAction("Index");
        }

        // ==========================================
        // GET: Patrocinadores/Agregar
        // ==========================================
        [HttpGet]
        public ActionResult Agregar()
        {
            var nuevoPatrocinador = new DatosPatrocinador();

            try
            {
                var coleccion = conexionDB.Database.GetCollection<DatosPatrocinador>("Patrocinadores");

                var ultimoPatrocinador = coleccion.Find(Builders<DatosPatrocinador>.Filter.Empty)
                                                 .SortByDescending(p => p.IdPatrocinador)
                                                 .FirstOrDefault();

                if (ultimoPatrocinador != null)
                {
                    nuevoPatrocinador.IdPatrocinador = ultimoPatrocinador.IdPatrocinador + 1;
                }
                else
                {
                    nuevoPatrocinador.IdPatrocinador = 800;
                }

                // Obtener Tipos de Soporte únicos para el Autocompletado de la vista
                var tiposSoporteSugeridos = coleccion.Find(_ => true).ToList()
                    .Where(p => !string.IsNullOrWhiteSpace(p.TipoPatrocinador))
                    .Select(p => p.TipoPatrocinador.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(t => t)
                    .ToList();

                ViewBag.TiposSugeridos = tiposSoporteSugeridos;
            }
            catch (Exception)
            {
                nuevoPatrocinador.IdPatrocinador = 800;
            }

            return View(nuevoPatrocinador);
        }

        // ==========================================
        // POST: Patrocinadores/Agregar
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Agregar(DatosPatrocinador nuevoPatrocinador)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var coleccion = conexionDB.Database.GetCollection<DatosPatrocinador>("Patrocinadores");

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

        // ==========================================
        // GET: Patrocinadores/Modificar/5
        // ==========================================
        [HttpGet]
        public ActionResult Modificar(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return RedirectToAction("Index");
            }

            try
            {
                var coleccion = conexionDB.Database.GetCollection<DatosPatrocinador>("Patrocinadores");
                var patrocinador = coleccion.Find(p => p.Id == id).FirstOrDefault();

                if (patrocinador == null)
                {
                    TempData["Error"] = "No se encontró el patrocinador especificado.";
                    return RedirectToAction("Index");
                }

                // Obtener Tipos de Soporte únicos para el Autocompletado de la vista
                var tiposSoporteSugeridos = coleccion.Find(_ => true).ToList()
                    .Where(p => !string.IsNullOrWhiteSpace(p.TipoPatrocinador))
                    .Select(p => p.TipoPatrocinador.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(t => t)
                    .ToList();

                ViewBag.TiposSugeridos = tiposSoporteSugeridos;

                return View(patrocinador);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar los datos: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // ==========================================
        // POST: Patrocinadores/Modificar
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Modificar(DatosPatrocinador patrocinadorActualizado)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var coleccion = conexionDB.Database.GetCollection<DatosPatrocinador>("Patrocinadores");
                    var filtro = Builders<DatosPatrocinador>.Filter.Eq(p => p.Id, patrocinadorActualizado.Id);
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

            return View(patrocinadorActualizado);
        }
    }
}
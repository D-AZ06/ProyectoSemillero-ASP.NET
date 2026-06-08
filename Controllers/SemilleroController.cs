using MongoDB.Bson;
using MongoDB.Driver;
using ProyectoSemillero_ASP.NET.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace ProyectoSemillero_ASP.NET.Controllers
{
    public class SemilleroController : Controller
    {
        private Conexion conexionDB = new Conexion();

        // GET: Semillero
        public ActionResult Index(string tipoFiltro, string valorFiltro)
        {
            try
            {
                if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");

                string rolUsuario = Session["Rol"].ToString();
                var coleccionSemilleros = conexionDB.Database.GetCollection<DatosSemillero>("Semilleros");

                var builder = Builders<DatosSemillero>.Filter;
                FilterDefinition<DatosSemillero> filtroSeguridad;

                // 1. FILTRO DE SEGURIDAD (Roles)
                if (rolUsuario == "Administrador" || rolUsuario == "Admin")
                {
                    filtroSeguridad = builder.Empty; // Ve todos los semilleros
                }
                else
                {
                    if (Session["IdSemillero"] != null)
                    {
                        int idSemillero = (int)Session["IdSemillero"];
                        filtroSeguridad = builder.Eq(s => s.IdSemillero, idSemillero); // Solo ve su semillero
                    }
                    else
                    {
                        TempData["Error"] = "Tu usuario no tiene un semillero asignado.";
                        return View(new List<DatosSemillero>());
                    }
                }

                // 2. FILTRO DE BÚSQUEDA
                FilterDefinition<DatosSemillero> filtroBusqueda = builder.Empty;

                if (!string.IsNullOrEmpty(tipoFiltro) && !string.IsNullOrEmpty(valorFiltro))
                {
                    switch (tipoFiltro)
                    {
                        case "idSemillero":
                            if (int.TryParse(valorFiltro, out int idSem))
                                filtroBusqueda = builder.Eq(s => s.IdSemillero, idSem);
                            break;
                        case "nombreSemillero":
                            filtroBusqueda = builder.Regex(s => s.nombreSemillero, new BsonRegularExpression(valorFiltro, "i"));
                            break;
                        case "lineaSemillero":
                            filtroBusqueda = builder.Regex(s => s.LineaSemillero, new BsonRegularExpression(valorFiltro, "i"));
                            break;
                        case "enfoqueSemillero":
                            filtroBusqueda = builder.Regex(s => s.EnfoqueSemillero, new BsonRegularExpression(valorFiltro, "i"));
                            break;
                    }
                }

                // 3. COMBINAR FILTROS Y BUSCAR
                var filtroFinal = builder.And(filtroSeguridad, filtroBusqueda);
                List<DatosSemillero> listaFinal = coleccionSemilleros.Find(filtroFinal).ToList();

                return View(listaFinal);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ocurrió un error al cargar la lista: " + ex.Message;
                return View(new List<DatosSemillero>());
            }
        }

        // GET: Semillero/Agregar
        [HttpGet]
        public ActionResult Agregar()
        {
            if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");
            string rolUsuario = Session["Rol"].ToString();

            // Solo el Admin puede crear semilleros nuevos
            if (rolUsuario != "Administrador" && rolUsuario != "Admin")
            {
                TempData["Error"] = "Solo los Administradores pueden crear nuevos semilleros.";
                return RedirectToAction("Index");
            }

            var coleccionSemilleros = conexionDB.Database.GetCollection<DatosSemillero>("Semilleros");
            var ultimoSemillero = coleccionSemilleros.Find(new BsonDocument()).SortByDescending(s => s.IdSemillero).FirstOrDefault();
            int correlativo = 1;

            if (ultimoSemillero != null)
            {
                string ultimoIdStr = ultimoSemillero.IdSemillero.ToString();
                if (ultimoIdStr.StartsWith("20"))
                {
                    if (int.TryParse(ultimoIdStr.Substring(2), out int numero)) correlativo = numero + 1;
                }
            }

            var nuevoSemillero = new DatosSemillero();
            nuevoSemillero.IdSemillero = int.Parse("20" + correlativo.ToString());

            return View(nuevoSemillero);
        }

        // POST: Semillero/Agregar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Agregar(DatosSemillero nuevoSemillero)
        {
            if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");
            if (Session["Rol"].ToString() != "Administrador" && Session["Rol"].ToString() != "Admin") return RedirectToAction("Index");

            ModelState.Remove("IdSemillero");

            if (ModelState.IsValid)
            {
                var coleccionSemilleros = conexionDB.Database.GetCollection<DatosSemillero>("Semilleros");

                var ultimoSemillero = coleccionSemilleros.Find(new BsonDocument()).SortByDescending(s => s.IdSemillero).FirstOrDefault();
                int correlativo = 1;

                if (ultimoSemillero != null)
                {
                    string ultimoIdStr = ultimoSemillero.IdSemillero.ToString();
                    if (ultimoIdStr.StartsWith("20"))
                    {
                        if (int.TryParse(ultimoIdStr.Substring(2), out int numero)) correlativo = numero + 1;
                    }
                }

                nuevoSemillero.IdSemillero = int.Parse("20" + correlativo.ToString());

                coleccionSemilleros.InsertOne(nuevoSemillero);
                TempData["Exito"] = $"Semillero '{nuevoSemillero.nombreSemillero}' registrado correctamente con el ID: {nuevoSemillero.IdSemillero}";
                return RedirectToAction("Index");
            }
            return View(nuevoSemillero);
        }

        // GET: Semillero/Modificar
        [HttpGet]
        public ActionResult Modificar(int id)
        {
            if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");
            string rolUsuario = Session["Rol"].ToString();

            if (rolUsuario == "Investigador" || rolUsuario == "Estudiante")
            {
                TempData["Error"] = "No tienes permisos para modificar información del semillero.";
                return RedirectToAction("Index");
            }

            if (rolUsuario == "Líder" && (int)Session["IdSemillero"] != id)
            {
                TempData["Error"] = "Solo puedes modificar la información de tu propio semillero.";
                return RedirectToAction("Index");
            }

            var coleccionSemilleros = conexionDB.Database.GetCollection<DatosSemillero>("Semilleros");
            var semillero = coleccionSemilleros.Find(s => s.IdSemillero == id).FirstOrDefault();

            if (semillero == null) return RedirectToAction("Index");

            return View(semillero);
        }

        // POST: Semillero/Modificar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Modificar(DatosSemillero semilleroModificado)
        {
            if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");
            string rolUsuario = Session["Rol"].ToString();

            if (rolUsuario == "Investigador" || rolUsuario == "Estudiante") return RedirectToAction("Index");

            if (ModelState.IsValid)
            {
                var coleccionSemilleros = conexionDB.Database.GetCollection<DatosSemillero>("Semilleros");
                var filtro = Builders<DatosSemillero>.Filter.Eq(s => s.IdSemillero, semilleroModificado.IdSemillero);

                if (rolUsuario == "Líder")
                {
                    int idSemilleroLider = (int)Session["IdSemillero"];
                    filtro = Builders<DatosSemillero>.Filter.And(
                        Builders<DatosSemillero>.Filter.Eq(s => s.IdSemillero, semilleroModificado.IdSemillero),
                        Builders<DatosSemillero>.Filter.Eq(s => s.IdSemillero, idSemilleroLider)
                    );
                }

                var resultado = coleccionSemilleros.ReplaceOne(filtro, semilleroModificado);
                if (resultado.MatchedCount > 0) TempData["Exito"] = "La información del semillero se ha actualizado.";
                else TempData["Error"] = "No se pudo actualizar o no tienes permisos.";

                return RedirectToAction("Index");
            }
            return View(semilleroModificado);
        }

        // GET: Semillero/Eliminar
        public ActionResult Eliminar(int id)
        {
            if (Session["Rol"] == null) return RedirectToAction("IniciarSesion", "Home");
            string rolUsuario = Session["Rol"].ToString();

            if (rolUsuario != "Administrador" && rolUsuario != "Admin")
            {
                TempData["Error"] = "Solo los Administradores pueden eliminar semilleros enteros.";
                return RedirectToAction("Index");
            }

            var coleccionSemilleros = conexionDB.Database.GetCollection<DatosSemillero>("Semilleros");
            var resultado = coleccionSemilleros.DeleteOne(s => s.IdSemillero == id);

            if (resultado.DeletedCount > 0) TempData["Exito"] = "El semillero ha sido eliminado permanentemente.";
            else TempData["Error"] = "No se pudo eliminar el semillero.";

            return RedirectToAction("Index");
        }
    }
}
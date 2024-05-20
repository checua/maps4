﻿const _modeloEmpleado = {
    idEmpleado: 0,
    nombreCompleto: "",
    idDepartamento: 0,
    sueldo: 0,
    fechaContrato: ""
}
document.addEventListener("DOMContentLoaded", function () {

    //MostrarEmpleados();

    fetch("/Home/listaTipoPropiedades")
        .then(response => {
            return response.ok ? response.json() : Promise.reject(response)
        })
        .then(responseJson => {

            if (responseJson.length > 0) {
                responseJson.forEach((item) => {

                    $("#cboTipoPropiedad").append(
                        $("<option>").val(item.idTipoPropiedad).text(item.nombre)
                    )

                })
            }

        })

    //$("#txtFechaContrato").datepicker({
    //    format: "dd/mm/yyyy",
    //    autoclose: true,
    //    todayHighlight: true
    //})


}, false)



function MostrarModal() {

    //$("#txtNombreCompleto").val(_modeloEmpleado.nombreCompleto);
    //$("#cboDepartamento").val(_modeloEmpleado.idDepartamento == 0 ? $("#cboDepartamento option:first").val() : _modeloEmpleado.idDepartamento)
    //$("#txtSueldo").val(_modeloEmpleado.sueldo);
    //$("#txtFechaContrato").val(_modeloEmpleado.fechaContrato)


    $("#modalEmpleado").modal("show");

}

$(document).on("click", ".boton-nuevo-acceso", function () {

    //_modeloEmpleado.idEmpleado = 0;
    //_modeloEmpleado.nombreCompleto = "";
    //_modeloEmpleado.idDepartamento = 0;
    //_modeloEmpleado.sueldo = 0;
    //_modeloEmpleado.fechaContrato = "";

    MostrarModal();

})

$(document).on("click", ".boton-iniciar-sesion", function () {

    const modelo = {
        correo: $("#correo").val(),
        contra: $("#contra").val()
    }

    //fetch("/Inicio/IniciarSesion", {
    //        method: "GET",
    //        headers: { "Content-Type": "application/json; charset=utf-8" },
    //        body: JSON.stringify(modelo)
    //    })
    //        .then(response => {
    //            return response.ok ? response.json() : Promise.reject(response)
    //        })
    //        .then(responseJson => {

    //            if (responseJson.valor) {
    //                $("#modalEmpleado").modal("hide");
    //                Swal.fire("Bienvenido", "success");
    //                MostrarEmpleados();
    //            }
    //            else
    //                Swal.fire("Lo sentimos", "No se puedo crear", "error");
    //        })

    fetch("/Inicio/IniciarSesion?correo=" + $("#correo").val() + "&contra=" + $("#contra").val())
        .then(response => {
            return response.ok ? response.json() : Promise.reject(response)
        })
        .then(responseJson => {

            if (responseJson.length > 0) {
                responseJson.forEach((item) => {

                    //$("#cboTipoPropiedad").append(
                    //    $("<option>").val(item.idTipoPropiedad).text(item.nombre)
                    //)
                    $("#modalEmpleado").modal("hide");

                    Swal.fire("Listo!", "Usuario logegado", "success");

                })
            }
            else {
                Swal.fire("Error!", "Usuario no encontrado", "danger");
            }

        })
        //.then((data) => {
        //    let credentials = data;

        //    authors.map(function (credentials) {
        //        let correo = $("#correo").val();
        //        let contra = $("#contra").val();
        //    });
        //})

    //const modelo = {
    //    idEmpleado: _modeloEmpleado.idEmpleado,
    //    nombreCompleto: $("#txtNombreCompleto").val(),
    //    refDepartamento: {
    //        idDepartamento: $("#cboDepartamento").val()
    //    },
    //    sueldo: $("#txtSueldo").val(),
    //    fechaContrato: $("#txtFechaContrato").val()
    //}


    //if (_modeloEmpleado.idEmpleado == 0) {

    //    fetch("/Home/guardarEmpleado", {
    //        method: "POST",
    //        headers: { "Content-Type": "application/json; charset=utf-8" },
    //        body: JSON.stringify(modelo)
    //    })
    //        .then(response => {
    //            return response.ok ? response.json() : Promise.reject(response)
    //        })
    //        .then(responseJson => {

    //            if (responseJson.valor) {
    //                $("#modalEmpleado").modal("hide");
    //                Swal.fire("Listo!", "Empleado fue creado", "success");
    //                MostrarEmpleados();
    //            }
    //            else
    //                Swal.fire("Lo sentimos", "No se puedo crear", "error");
    //        })

    //} else {

    //    fetch("/Home/editarEmpleado", {
    //        method: "PUT",
    //        headers: { "Content-Type": "application/json; charset=utf-8" },
    //        body: JSON.stringify(modelo)
    //    })
    //        .then(response => {
    //            return response.ok ? response.json() : Promise.reject(response)
    //        })
    //        .then(responseJson => {

    //            if (responseJson.valor) {
    //                $("#modalEmpleado").modal("hide");
    //                Swal.fire("Listo!", "Empleado fue actualizado", "success");
    //                MostrarEmpleados();
    //            }
    //            else
    //                Swal.fire("Lo sentimos", "No se puedo actualizar", "error");
    //        })

    //}


})
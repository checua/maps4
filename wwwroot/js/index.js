const _modeloEmpleado = {
    idEmpleado: 0,
    nombreCompleto: "",
    idDepartamento: 0,
    sueldo: 0,
    fechaContrato: ""
}
document.addEventListener("DOMContentLoaded", function () {

    var latLng = new google.maps.LatLng(24.02, -104.62);
    var opciones = {
        center: latLng,
        zoom: 12,
        mapTypeId: google.maps.MapTypeId.roadmap,
        disableDefaultUI: true
    };

    var map = new google.maps.Map(document.getElementById('map_canvas'), opciones);
    geocoder = new google.maps.Geocoder();
    infowindow = new google.maps.InfoWindow();

    fetch("/Home/listaInmuebles")
        .then(response => {
            return response.ok ? response.json() : Promise.reject(response)
        })
        .then(responseJson => {

            if (responseJson.length > 0) {

                responseJson.forEach((item) => {
                    switch (item.idTipo) {
                        case 0:
                            imageIcon = "images/icon/casa_che.png";
                            break;
                        case 1:
                            imageIcon = "images/icon/casa_che.png";
                            break;
                        case 2:
                            imageIcon = "images/icon/casa.png";
                            break;
                        case 3:
                            imageIcon = "images/icon/apartment1.png";
                            break;
                        case 4:
                            imageIcon = "images/icon/apartment2.png";
                            break;
                        case 5:
                            imageIcon = "images/icon/terreno1b.png";
                            break;
                        case 6:
                            imageIcon = "images/icon/terreno2.png";
                            break
                        case 7:
                            imageIcon = "images/icon/local.png";
                            break
                        case 8:
                            imageIcon = "images/icon/local2.png";
                            break
                        case 9:
                            imageIcon = "images/icon/building.png";
                            break
                        case 10:
                            imageIcon = "images/icon/building2.png";
                            break
                        case 11:
                            imageIcon = "images/icon/montacargas2.png";
                            break
                        case 12:
                            imageIcon = "images/icon/montacargas.png";
                            break
                        case 13:
                            imageIcon = "images/icon/desk.png";
                            break
                        case 14:
                            imageIcon = "images/icon/desk2.png";
                            break
                        case 15:
                            imageIcon = "images/icon/tractor2.png";
                            break
                        case 16:
                            imageIcon = "images/icon/tractor3.png";
                            break
                    }

                    var latLng = new google.maps.LatLng(item.lat, item.lng);
                    new google.maps.Marker({
                        position: latLng,
                        map,
                        title: String(item.idTipo),
                        icon: {
                            url: imageIcon,
                            scaledSize: new google.maps.Size(32, 32)
                        },
                    });
                })
                map.setCenter(latLng);
            }
        })

    google.maps.event.addListener(map, 'mousedown', function (e) {

        var latLng = e.latLng;

        marker = new google.maps.Marker({
            position: latLng,
            map: map,
            draggable: false
        });

        if (mousedUp === false) {
            var log = document.getElementById("lnkAcceso").innerText;

            //if (log == "Iniciar Sesión")
            //    alert("Ingresa para poder registrar propiedades");
            //else {
            //    alert("Bienvenido");
            //}


            if (log == "Iniciar Sesión")
                alert("Ingresa para poder registrar propiedades");
            else {
                /*alert(log);*/
                geocoder.geocode({ 'latLng': event.latLng }, function (results, status) {

                    /*alert("Geocode");*/
                    if (status == google.maps.GeocoderStatus.OK) {

                        alert("Status ok");

                        //if (results[0]) {
                        //    alert("No result 0");
                        //    ////            document.getElementById("hfCoordenadas").value = results[0].geometry.location;
                        //    ////            document.getElementById("hfLat").value = event.latLng.lat().toFixed(6);
                        //    ////            document.getElementById("hfLng").value = event.latLng.lng().toFixed(6);
                        //    ////            document.getElementById("hfDireccion").value = results[0].formatted_address;

                        //    ////            marker = new google.maps.Marker({
                        //    ////                position: event.latLng,
                        //    ////                map: map
                        //    ////            });

                        //    ////            GetCode2();

                        //} else {
                        //    ////            document.getElementById('geocoding').innerHTML =
                        //    ////                'No se encontraron resultados';
                        //    alert("No result 0");
                        //}

                    }
                    else {
                        document.getElementById("lnkAcceso").innerHTML = 'Geocodificación  ha fallado debido a: ' + status;
                    }
                });
            }

        }

    });

    //MostrarEmpleados();

    $.ajax({
        url: '/Home/GetUserClaims',
        type: 'GET',
        success: function (data) {
            // Maneja los claims devueltos aquí
            //console.log(data);
            $("#lnkAcceso").text(data[0].value);
        },
        error: function () {
            console.error('Error al obtener los claims del usuario.');
        }
    });



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
    fetch("/Inicio/IniciarSesion?correo=" + $("#correo").val() + "&contra=" + $("#contra").val())
        .then(response => {
            return response.ok ? response.json() : Promise.reject(response)
        })
        .then(responseJson => {
            if (responseJson.length > 0) {
                responseJson.forEach((item) => {
                    $("#modalEmpleado").modal("hide");
                    $("#lnkAcceso").text(item.correo);
                    //Swal.fire("Listo! " + item.correo, "Usuario logegado", "success");
                })
            }
            else {
                Swal.fire("Error!", "Usuario no encontrado", "danger");
            }
        })
})


//$(document).on("click", ".boton-iniciar-sesion", function () {
//    fetch("/Inicio/IniciarSesion?correo=" + $("#correo").val() + "&contra=" + $("#contra").val())
//        .then(response => {
//            return response.ok ? response.json() : Promise.reject(response)
//        })
//        .then(responseJson => {
//            if (responseJson.length > 0) {
//                responseJson.forEach((item) => {
//                    $("#modalEmpleado").modal("hide");
//                    $("#lnkAcceso").text(item.correo);
//                    //Swal.fire("Listo! " + item.correo, "Usuario logegado", "success");
//                })
//            }
//            else {
//                Swal.fire("Error!", "Usuario no encontrado", "danger");
//            }
//        })
//})


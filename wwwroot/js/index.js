var map;
var geocoder;
var infowindow;
var marker;
var markerx;
var markers = [];
var markersx = [];
var banSiExc = 1;
var banNoExc = 1;
var banCasaVenta = 1;
var banCasaRenta = 1;
var banTerreVenta = 1;
var mousedUp = false;
var degrees = 90;
var img = null;
var canvas = null;
var access = null;
var i = 0;
var latLng;
var latLngx;
var typeofp;

let currentInmuebleId = null;

// Función para cargar la API de Google Maps de manera asincrónica
function loadGoogleMapsAPI(apiKey) {
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = `https://maps.googleapis.com/maps/api/js?key=${apiKey}&callback=initMap&libraries=&v=weekly&channel=2&loading=async`;
        script.async = true;
        script.defer = true;
        script.onerror = () => reject(new Error('Error loading Google Maps API'));
        window.initMap = () => {
            resolve();
            initializeMap(); // Inicializar el mapa una vez que la API se haya cargado
        };
        document.head.appendChild(script);
    });
}

// Función para inicializar el mapa
function initializeMap() {
    const latLng = new google.maps.LatLng(24.017926, -104.657079);
    const opciones = {
        center: latLng,
        zoom: 12,
        mapTypeId: google.maps.MapTypeId.roadmap,
        disableDefaultUI: true
    };

    map = new google.maps.Map(document.getElementById('map_canvas'), opciones);
    geocoder = new google.maps.Geocoder();
    infowindow = new google.maps.InfoWindow();

    google.maps.event.addListener(map, 'mousedown', function (e) {
        mousedUp = false;
        setTimeout(function () {

            var latLng1 = e.latLng;

            if (mousedUp === false) {
                var log = document.getElementById("lnkAcceso").innerText;

                if (log != "Iniciar Sesión") //Le cambie para no batallar en entrar, pero hay que regresar a ==
                    //alert(log);
                    geocoder.geocode({ 'latLng': latLng1 }, function (results, status) {

                        //alert("Geocode");
                        if (status == google.maps.GeocoderStatus.OK) {

                            //alert("Status ok");

                            if (results[0]) {

                                var location1 = results[0].geometry.location;
                                var lat1 = latLng1.lat().toFixed(6);
                                var lng1 = latLng1.lng().toFixed(6);
                                var adress1 = results[0].formatted_address;

                                marker = new google.maps.Marker({
                                    position: latLng1,
                                    map: map
                                });

                                markers[i] = marker;
                                i++;

                                var label = document.getElementById('ubicacion');
                                label.textContent = "(" + lat1 + "," + lng1 + ")";
                                //alert("Si result 1");
                                //alert(location1 + " " + lat1 + " " + lat2 + " " + adress1);
                                $('#btnClear').show();
                                $('.btn-fileupload').show();
                                $('.boton-guardar-inmueble').show();
                                $('#contacto_a').show();
                                $(".boton-eliminar-inmueble").hide();

                                GetCode2();
                            } else {
                                // document.getElementById('geocoding').innerHTML ='No se encontraron resultados';
                                alert("No result 0");
                            }
                        }
                        else {
                            document.getElementById("lnkAcceso").innerHTML = 'Geocodificación  ha fallado debido a: ' + status;
                        }
                    });
                else {
                    alert("Ingresa para poder registrar propiedades");
                }
            }
        }, 500);

    });
    google.maps.event.addListener(map, 'mouseup', function (event) {
        mousedUp = true;
    });
    google.maps.event.addListener(map, 'dragstart', function (event) {
        mousedUp = true;
    });
    google.maps.event.addListener(map, 'touchstart', function (event) {
        mousedUp = true;
    });

    fetchMarkers();
}

document.addEventListener('DOMContentLoaded', () => {
    const apiKey = 'AIzaSyAiEPIpKUewZTon1DeYNod3M63NB_HRcU4';  // Reemplaza con tu clave API de Google Maps
    loadGoogleMapsAPI(apiKey)
        .then(() => {
            console.log('Google Maps API loaded successfully');
        })
        .catch(error => {
            console.error('Error loading Google Maps API:', error);
        });



});

function fetchMarkers() {
    fetch("/Home/listaInmuebles")
        .then(response => {
            return response.ok ? response.json() : Promise.reject(response)
        })
        .then(responseJson => {
            if (responseJson.length > 0) {
                responseJson.forEach((item) => {
                    let imageIcon;
                    switch (item.idTipo) {
                        case 2: {
                            imageIcon = "images/icon/casa_che.png";
                            typeofp = "Casa en venta";
                            break;
                        }
                        case 3: {
                            imageIcon = "images/icon/casa.png";
                            typeofp = "Casa en renta";
                            break;
                        }
                        case 4: {
                            imageIcon = "images/icon/apartment1.png";
                            typeofp = "Apartamento en venta";
                            break;
                        }
                        case 5: {
                            imageIcon = "images/icon/apartment2.png";
                            typeofp = "Apartamento en renta";
                            break;
                        }
                        case 6: {
                            imageIcon = "images/icon/terreno1b.png";
                            typeofp = "Terreno en venta";
                            break;
                        }
                        case 7: {
                            imageIcon = "images/icon/terreno2.png";
                            typeofp = "Terreno en renta";
                            break;
                        }
                        case 8: {
                            imageIcon = "images/icon/local.png";
                            typeofp = "Local en venta";
                            break;
                        }
                        case 9: {
                            imageIcon = "images/icon/local2.png";
                            typeofp = "Local en renta";
                            break;
                        }
                        case 10: {
                            imageIcon = "images/icon/building.png";
                            typeofp = "Edificio en venta";
                            break;
                        }
                        case 11: {
                            imageIcon = "images/icon/building2.png";
                            typeofp = "Edificio en renta";
                            break;
                        }
                        case 12: {
                            imageIcon = "images/icon/montacargas2.png";
                            typeofp = "Bodega en venta";
                            break;
                        }
                        case 13: {
                            imageIcon = "images/icon/montacargas.png";
                            typeofp = "Bodega en renta";
                            break;
                        }
                        case 14: {
                            imageIcon = "images/icon/desk.png";
                            typeofp = "Oficina en venta";
                            break;
                        }
                        case 15: {
                            imageIcon = "images/icon/desk2.png";
                            typeofp = "Oficina en renta";
                            break;
                        }
                        case 16: {
                            imageIcon = "images/icon/tractor1.png";
                            typeofp = "Rancho en venta";
                            break;
                        }
                        case 17: {
                            imageIcon = "images/icon/tractor2.png";
                            typeofp = "Rancho en renta";
                            break;
                        }
                    }

                    const latLng = new google.maps.LatLng(item.lat, item.lng);
                    const markerx = new google.maps.Marker({
                        position: latLng,
                        map,
                        title: String(item.idTipo),
                        //title: String("Id Inmueble: " + item.idInmueble + ", Propiedad: " + typeofp), //Con esto no filtra, luego lo vemos
                        icon: {
                            url: imageIcon,
                            scaledSize: new google.maps.Size(32, 32),
                            origin: new google.maps.Point(0, 0), // Origen de la imagen (0, 0)
                            anchor: new google.maps.Point(16, 16) // Punto de anclaje de la imagen (centrado)
                        },
                    });

                    markerx.customInfo = item.precio;
                    markersx[i] = markerx;
                    i++;

                    markerx.addListener('click', function () {
                        $('#btnClear').css('display', "none");
                        $('.btn-fileupload').css('display', "none");
                        $('.boton-guardar-inmueble').css('display', "none");

                        const str = document.getElementById("lnkAcceso").innerText;
                        const str2 = item.refUsuario.correo.toString();

                        const res = str.toUpperCase();
                        const res2 = str2.toUpperCase();

                        if (res != res2) {
                            $('.boton-eliminar-inmueble').css('display', "none");
                            $("#contacto_a").hide(); 
                        } else {
                            $(".boton-eliminar-inmueble").show();
                            $("#contacto_a").show(); 
                        }

                        var nom_tel = item.refUsuario.nombres + " " + item.refUsuario.aPaterno;
                        GetCode1(item.idTipo, item.idInmueble, nom_tel , item.telefono, item.terreno, item.construccion, item.precio, item.observaciones, item.contacto, item.imagenes);
                    });
                });
                map.setCenter(latLng);
            }
        })
        .catch(error => {
            console.error('Error al obtener la lista de inmuebles:', error);
        });
}

    
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

                if ($("#cboTipoPropiedad option").length > 1) {
                    $("#tipo").append(
                        $("<option>").val(item.idTipoPropiedad).text(item.nombre)
                    );
                }

            })
        }

    })

fetch('/Home/GetUserClaims', {
    method: 'GET'
})
    .then(response => {
        if (!response.ok) {
            throw new Error('Network response was not ok');
        }
        return response.json();
    })
    .then(data => {
        if (!data || data.length === 0 || data === 0) {
            // Si los datos son 0, no hacer nada
            return;
        }
        // Maneja los claims devueltos aquí
        document.getElementById('lnkAcceso').textContent = data[0].value;
        var access = data[0].value;
    })
    .catch(error => {
        console.error('Error al obtener los claims del usuario:', error);
    });






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
                    $("#correo").val("");
                    $("#contra").val("");
                })
            }
            else {
                Swal.fire("Error!", "Usuario no encontrado", "danger");
            }
        })
        .catch(error => {
            //console.error('Error al obtener los claims del usuario:', error);
            Swal.fire("Error!", "Usuario o contraseña equivocados", "danger");
        });
})

function numberWithCommas(x) {
    if (typeof x !== 'number' && typeof x !== 'string') {
        return x;  // Devuelve el valor sin cambios si no es un número o una cadena
    }
    var parts = x.toString().split('.');
    parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    return parts.join('.');
}

function panel(imgElement) {
    // Mostrar el modal de imagen completa
    $('#modalImagenCompleta').modal('show');
}

function GetCode1(a, b, c, d, e, f, g, h, i, j) {

    currentInmuebleId = b; // Guarda el id del inmueble actual

    $('#tipo').val(a);
    document.getElementById("telefono").innerHTML = c + " " + d;
    $('#terreno').val(numberWithCommas(e));
    $('#construccion').val(numberWithCommas(f));
    $('#precio').val("$ " + numberWithCommas(g));
    if (h != 0)
        $('#descripcion').val(h);
    else
        $('#descripcion').val("");
    if (i != 0)
        $('#contacto_a').val(i);
    else
        $('#contacto_a').val("");
    document.getElementById("ubicacion").innerHTML = "";//j;

    $('#imgViewer').remove();
    $('#imageStrip').empty(); // Limpiar el contenedor antes de añadir nuevas imágenes

    $('div#test1').append('<div id="imgViewer" class="slider"></div>');
    if (j != 0) {
        for (var x = 1; x <= j; x++) {
            var imgSrc = `Cargas/${b}_${x}.jpg`;
            var imgElement = `
                <div id="images${x}" class="item">
                    <img src="${imgSrc}" onclick="panel(this);" id="_${b}_${x}" style="margin-left:2px; max-height:50px !important;">
                </div>
            `;
            $('#imgViewer').append(imgElement);

            // Añadir la imagen a la tira horizontal
            var stripItem = `
                <img src="${imgSrc}" class="d-block" style="max-height:500px; margin:auto;">
            `;
            $('#imageStrip').append(stripItem);
        }
    } else {
        $('#imgViewer').append($('<img>', { src: "images/nohouse.jpg", width: '50px', height: '50px' }));
    }
    $("#modalInmueble").modal("show");
}

function GetCode2() {
    //$("#txtNombreCompleto").val(_modeloEmpleado.nombreCompleto);
    //$("#cboDepartamento").val(_modeloEmpleado.idDepartamento == 0 ? $("#cboDepartamento option:first").val() : _modeloEmpleado.idDepartamento)
    //$("#txtSueldo").val(_modeloEmpleado.sueldo);
    //$("#txtFechaContrato").val(_modeloEmpleado.fechaContrato)
    $("#modalInmueble").modal("show");
}

function ShowImagePreview(evt) {
    var files = evt.files;
    if (files.length) {
        var filePromises = [];
        for (var i = 0, f; f = files[i]; i++) {
            // Verificar si el archivo es una imagen
            if (f.type.startsWith('image/')) {
                filePromises.push(resizeImageFile(f));
            } else {
                console.warn(`File ${f.name} is not an image and will be discarded.`);
            }
        }

        Promise.all(filePromises).then(function (resizedFiles) {
            // Crear un nuevo DataTransfer para actualizar el input file
            var dataTransfer = new DataTransfer();
            resizedFiles.forEach(function (file) {
                dataTransfer.items.add(file);
            });

            // Reemplazar los archivos en el input file con los archivos redimensionados
            evt.files = dataTransfer.files;

            // Mostrar miniaturas
            for (var file of resizedFiles) {
                var reader = new FileReader();
                reader.onload = function (e) {
                    var imgElement = document.createElement("img");
                    imgElement.src = e.target.result;
                    imgElement.style.height = "100px";  // Estilo opcional para fijar altura
                    imgElement.style.marginBottom = "2px";  // Margen inferior opcional
                    imgElement.style.marginRight = "2px";  // Margen derecho opcional
                    imgElement.style.display = "inline-block";  // Mostrar como bloque en línea
                    document.getElementById('imgViewer').appendChild(imgElement);
                };
                reader.readAsDataURL(file);
            }
        });
    } else {
        alert("Failed to load files");
    }
}

async function resizeImageFile(file) {
    return new Promise(function (resolve) {
        var reader = new FileReader();
        reader.onload = function (e) {
            var img = new Image();
            img.onload = function () {
                var maxWidth = 1000;  // Tamaño máximo deseado
                var maxHeight = 1000;
                var width = img.width;
                var height = img.height;

                // Redimensionar la imagen si es necesario
                if (width > maxWidth || height > maxHeight) {
                    if (width > height) {
                        height *= maxWidth / width;
                        width = maxWidth;
                    } else {
                        width *= maxHeight / height;
                        height = maxHeight;
                    }
                }

                // Crear un canvas para redimensionar la imagen
                var canvas = document.createElement('canvas');
                canvas.width = width;
                canvas.height = height;
                var ctx = canvas.getContext('2d');
                ctx.drawImage(img, 0, 0, width, height);

                // Obtener el data URI de la imagen redimensionada
                var dataUri = canvas.toDataURL('image/jpeg', 0.7);  // Calidad ajustada a 0.7

                // Convertir data URI a Blob y luego a File
                fetch(dataUri).then(res => res.blob()).then(function (blob) {
                    var resizedFile = new File([blob], file.name, { type: 'image/jpeg' });
                    resolve(resizedFile);
                });
            };
            img.src = e.target.result;
        };
        reader.readAsDataURL(file);
    });
}

function clearPreviewAndFields() {
    // Limpiar imágenes del contenedor de vista previa
    document.getElementById('imgViewer').innerHTML = '';

    // Limpiar campos de entrada
    document.getElementById('tipo').value = '';
    document.getElementById('terreno').value = '';
    document.getElementById('construccion').value = '';
    document.getElementById('precio').value = '';
    document.getElementById('descripcion').value = '';
    document.getElementById('contacto_a').value = '';
    document.getElementById("telefono").innerHTML = "";

    // Ocultar el botón "Limpiar"
    document.getElementById('btnClear').style.display = 'none';
}

// Limpia el modal cuando se cierra
document.getElementById('modalInmueble').addEventListener('hidden.bs.modal', function () {
    clearPreviewAndFields();
});

function clearAll() {
    markers.forEach(function (marker) {
        marker.setMap(null);
    });

    markers = [];

    // Limpiar imágenes del contenedor de vista previa
    document.getElementById('imgViewer').innerHTML = '';

    // Limpiar campos de entrada
    document.getElementById('tipo').value = '';
    document.getElementById('terreno').value = '';
    document.getElementById('construccion').value = '';
    document.getElementById('precio').value = '';
    document.getElementById('descripcion').value = '';
    document.getElementById('contacto_a').value = '';
    document.getElementById("telefono").innerHTML = "";

    // Ocultar el botón "Limpiar"
    document.getElementById('btnClear').style.display = 'none';

    // Opcional: También podrías limpiar cualquier otro estado o realizar otras acciones necesarias
}

document.getElementById('modalInmueble').addEventListener('hidden.bs.modal', function () {
    clearAll();
});

//function clearPreviewAndFields() {
//    document.getElementById('tipo').value = '';
//    document.getElementById('terreno').value = '';
//    document.getElementById('construccion').value = '';
//    document.getElementById('imgViewer').innerHTML = '';
//}



//$(document).on("click", ".boton-guardar-inmueble", function () {
//    const formData = new FormData();

//    const ubicacionTexto = document.getElementById('ubicacion').textContent;
//    const { lat, lng } = extractLatLon(ubicacionTexto);

//    // Agrega datos del modelo al FormData
//    formData.append('Datax.Lat', lat.toFixed(6));  // Asegúrate de que tenga 6 decimales
//    formData.append('Datax.Lng', lng.toFixed(6));  // Asegúrate de que tenga 6 decimales
//    formData.append('Datax.IdTipo', $("#tipo").val());
//    formData.append('Datax.Terreno', $("#terreno").val());
//    formData.append('Datax.Construccion', $("#construccion").val());
//    formData.append('Datax.Precio', $("#precio").val());
//    formData.append('Datax.Observaciones', $("#descripcion").val());
//    formData.append('Datax.Contacto', $("#contacto_a").val());

//    // Agrega los archivos al FormData
//    const files = document.getElementById("FileUpload1").files;
//    for (let i = 0; i < files.length; i++) {
//        formData.append('Files', files[i]);
//    }

//    formData.append('Correo', document.getElementById("lnkAcceso").innerText);

//    fetch("/Inmueble/RegistrarInmueble", {
//        method: 'POST',
//        body: formData  // Nota que no establecemos Content-Type. FormData lo hará automáticamente.
//    })
//        .then(response => response.json())
//        .then(response => {
//            if (response.success) {
//                //alert(response.message);
//                location.reload();
//            } else {
//                alert("Error al guardar el inmueble y las imágenes");
//            }
//        })
//        .catch(error => {
//            console.error('Error al enviar datos:', error);
//            alert("Error en la red o servidor");
//        });
//});

//// Función para extraer latitud y longitud del texto del label
//function extractLatLon(text) {
//    const parts = text.match(/\(([\d.-]+),\s*([\d.-]+)\)/);
//    if (parts) {
//        let lat = parseFloat(parts[1]).toFixed(6);
//        let lng = parseFloat(parts[2]).toFixed(6);
//        return { lat: parseFloat(lat), lng: parseFloat(lng) };
//    }
//    return { lat: null, lng: null };
//}

function validateForm(event) {
    event.preventDefault(); // Prevenir el envío del formulario por defecto

    let isValid = true;

    const tipo = document.getElementById("tipo").value;
    const terreno = document.getElementById("terreno").value;
    const construccion = document.getElementById("construccion").value;
    const precio = document.getElementById("precio").value;

    if (!tipo) {
        isValid = false;
        document.getElementById("tipoError").textContent = "Seleccione el tipo de propiedad.";
        document.getElementById("tipo").classList.add("is-invalid");
    } else {
        document.getElementById("tipoError").textContent = "";
        document.getElementById("tipo").classList.remove("is-invalid");
    }

    if (!terreno) {
        isValid = false;
        document.getElementById("terrenoError").textContent = "El terreno es obligatorio.";
        document.getElementById("terreno").classList.add("is-invalid");
    } else {
        document.getElementById("terrenoError").textContent = "";
        document.getElementById("terreno").classList.remove("is-invalid");
    }

    if (!construccion) {
        isValid = false;
        document.getElementById("construccionError").textContent = "La construcción es obligatoria.";
        document.getElementById("construccion").classList.add("is-invalid");
    } else {
        document.getElementById("construccionError").textContent = "";
        document.getElementById("construccion").classList.remove("is-invalid");
    }

    if (!precio) {
        isValid = false;
        document.getElementById("precioError").textContent = "El precio es obligatorio.";
        document.getElementById("precio").classList.add("is-invalid");
    } else {
        document.getElementById("precioError").textContent = "";
        document.getElementById("precio").classList.remove("is-invalid");
    }

    if (isValid) {
        submitForm(); // Llamar a la función para enviar el formulario
    }
}

function submitForm() {
    const formData = new FormData();

    const ubicacionTexto = document.getElementById('ubicacion').textContent;
    const { lat, lng } = extractLatLon(ubicacionTexto);

    // Agrega datos del modelo al FormData
    formData.append('Datax.Lat', lat.toFixed(6));  // Asegúrate de que tenga 6 decimales
    formData.append('Datax.Lng', lng.toFixed(6));  // Asegúrate de que tenga 6 decimales
    formData.append('Datax.IdTipo', $("#tipo").val());
    formData.append('Datax.Terreno', $("#terreno").val());
    formData.append('Datax.Construccion', $("#construccion").val());
    formData.append('Datax.Precio', $("#precio").val());
    formData.append('Datax.Observaciones', $("#descripcion").val());
    formData.append('Datax.Contacto', $("#contacto_a").val());

    // Agrega los archivos al FormData
    const files = document.getElementById("FileUpload1").files;
    for (let i = 0; i < files.length; i++) {
        formData.append('Files', files[i]);
    }

    formData.append('Correo', document.getElementById("lnkAcceso").innerText);

    fetch("/Inmueble/RegistrarInmueble", {
        method: 'POST',
        body: formData  // Nota que no establecemos Content-Type. FormData lo hará automáticamente.
    })
        .then(response => response.json())
        .then(response => {
            if (response.success) {
                //alert(response.message);
                location.reload();
            } else {
                alert("Error al guardar el inmueble y las imágenes");
            }
        })
        .catch(error => {
            console.error('Error al enviar datos:', error);
            alert("Error en la red o servidor");
        });
}

function extractLatLon(text) {
    const parts = text.match(/\(([\d.-]+),\s*([\d.-]+)\)/);
    if (parts) {
        let lat = parseFloat(parts[1]).toFixed(6);
        let lng = parseFloat(parts[2]).toFixed(6);
        return { lat: parseFloat(lat), lng: parseFloat(lng) };
    }
    return { lat: null, lng: null };
}

function clearPreviewAndFields() {
    // Limpiar imágenes del contenedor de vista previa
    document.getElementById('imgViewer').innerHTML = '';

    // Limpiar campos de entrada
    document.getElementById('tipo').value = '';
    document.getElementById('terreno').value = '';
    document.getElementById('construccion').value = '';
    document.getElementById('precio').value = '';
    document.getElementById('descripcion').value = '';
    document.getElementById('contacto_a').value = '';

    // Limpiar mensajes de error
    document.getElementById("tipoError").textContent = "";
    document.getElementById("terrenoError").textContent = "";
    document.getElementById("construccionError").textContent = "";
    document.getElementById("precioError").textContent = "";

    // Remover clases de error
    document.getElementById("tipo").classList.remove("is-invalid");
    document.getElementById("terreno").classList.remove("is-invalid");
    document.getElementById("construccion").classList.remove("is-invalid");
    document.getElementById("precio").classList.remove("is-invalid");

    // Ocultar el botón "Limpiar"
    document.getElementById('btnClear').style.display = 'none';
}

// Limpiar campos y mensajes de error al cerrar el modal
document.getElementById('modalInmueble').addEventListener('hidden.bs.modal', function () {
    clearPreviewAndFields();
});


$(document).on("click", "#cerrarmodalImagenCompleta", function () {
    $("#modalImagenCompleta").modal("hide");
});

$(document).on("click", ".boton-eliminar-inmueble", function () {
    if (currentInmuebleId) {
        fetch("/Inmueble/Eliminar?idInmueble=" + currentInmuebleId, {
            method: 'DELETE',
            headers: {
                'Content-Type': 'application/json'
            }
        })
            .then(response => {
                if (!response.ok) {
                    throw new Error('Network response was not ok');
                }
                return response.json().catch(() => {
                    throw new Error('Invalid JSON response');
                });
            })
            .then(response => {
                if (response.success) {
                    location.reload();
                } else {
                    alert("Error al eliminar el inmueble");
                }
            })
            .catch(error => {
                console.error('Error al eliminar el inmueble:', error);
                alert("Error en la red o servidor");
            });
    } else {
        alert("No se ha seleccionado ningún inmueble para eliminar");
    }
});


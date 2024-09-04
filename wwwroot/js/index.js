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
let isUpdate;
let clikeado = 0;
let currentInmueble = null; 

let currentInmuebleId = null;
let selectedInmuebleId = null;



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

    const defaultLatLng = new google.maps.LatLng(24.017926, -104.657079); // Ubicación predeterminada

    const opciones = {
        center: defaultLatLng,
        zoom: 12,
        mapTypeId: google.maps.MapTypeId.roadmap,
        /*fullscreenControl: true, // Habilitar el control de pantalla completa*/
        disableDefaultUI: true
    };

    map = new google.maps.Map(document.getElementById('map_canvas'), opciones);
    geocoder = new google.maps.Geocoder();
    infowindow = new google.maps.InfoWindow();

    // Intentar obtener la ubicación del usuario
    if (navigator.geolocation) {
        navigator.geolocation.getCurrentPosition(
            function (position) {
                const userLatLng = new google.maps.LatLng(position.coords.latitude, position.coords.longitude);
                map.setCenter(userLatLng);

                // Añadir un marcador en la ubicación del usuario con un ícono personalizado
                new google.maps.Marker({
                    position: userLatLng,
                    map: map,
                    title: "Tu ubicación",
                    icon: {
                        path: google.maps.SymbolPath.CIRCLE,
                        scale: 5,
                        fillColor: '#4285F4',
                        fillOpacity: 1,
                        strokeWeight: 2,
                        strokeColor: '#ffffff'
                    }
                });
            },
            function () {
                handleLocationError(true, map.getCenter());
            }
        );
    } else {
        // Navegador no soporta geolocalización
        handleLocationError(false, map.getCenter());
    }

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
                                    $('.boton-guardar-inmueble').text('Guardar');
                                    $('.boton-guardar-inmueble').attr('onclick', 'validateForm(event, false)');
                                    isUpdate = false;
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
        }, 850);

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
    const queryParams = getQueryParams();
    if (queryParams.inmuebleId) {
        loadInmueble(queryParams.inmuebleId);
    }

    const apiKey = 'AIzaSyAiEPIpKUewZTon1DeYNod3M63NB_HRcU4';  // Reemplaza con tu clave API de Google Maps
    loadGoogleMapsAPI(apiKey)
        .then(() => {
            console.log('Google Maps API loaded successfully');
        })
        .catch(error => {
            console.error('Error loading Google Maps API:', error);
        });

});

function loadInmueble(inmuebleId) {
    fetch(`/Inmueble/GetInmuebleById?id=${inmuebleId}`)
        .then(response => response.json())
        .then(inmueble => {
            if (inmueble) {
                //const latLng = new google.maps.LatLng(clat, inmueble[0].lng);
                //map.setCenter(latLng);
                //const marker = createMarker(inmueble, latLng);
                //markers.push(marker);

                // Actualizar la variable currentInmueble
                currentInmueble = {
                    lat: inmueble[0].lat,
                    lng: inmueble[0].lng,
                    id: inmueble[0].idInmueble
                };

                const str = document.getElementById("lnkAcceso").innerText;
                const str2 = inmueble[0].refUsuario.correo.toString();

                const res = str.toUpperCase();
                const res2 = str2.toUpperCase();

                if (res != res2) {
                    $('.boton-eliminar-inmueble').css('display', "none");
                    $("#contacto_a").hide();
                    $('.boton-guardar-inmueble').css('display', "none");

                } else {
                    $("#contacto_a").show();
                    $('.boton-guardar-inmueble').css('display', "inline");
                    $(".boton-eliminar-inmueble").show();

                    // Cambiar el texto del botón y el evento onclick
                    if (inmueble) {  // Asumiendo que tienes una propiedad isExisting para verificar si es una actualización
                        $('.boton-guardar-inmueble').text('Actualizar');
                        $('.boton-guardar-inmueble').attr('onclick', 'validateForm(event, true)');
                        isUpdate = true;
                    } else {
                        $('.boton-guardar-inmueble').text('Guardar');
                        $('.boton-guardar-inmueble').attr('onclick', 'validateForm(event, false)');

                        isUpdate = false;
                    }
                }

                $('#btnClear').css('display', "none");
                $('.btn-fileupload').css('display', "none");
                //onMarkerClick(inmueble);
                selectedInmuebleId = inmuebleId;
                
                var nom_tel = inmueble[0].refUsuario.nombres + " " + inmueble[0].refUsuario.aPaterno;
                GetCode1(inmueble[0].idTipo, inmueble[0].idInmueble, nom_tel, inmueble[0].telefono, inmueble[0].terreno, inmueble[0].construccion, inmueble[0].precio, inmueble[0].observaciones, inmueble[0].contacto, inmueble[0].imagenes);

            } else {
                alert('Inmueble no encontrado');
            }
        })
        .catch(error => console.error('Error al cargar el inmueble:', error));
}

function gotoLocation() {
    if (currentInmueble) {
        
        const lat = currentInmueble.lat;
        const lng = currentInmueble.lng;
        const latLng = new google.maps.LatLng(lat, lng);
        const isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent) && !window.MSStream;
        const url = isIOS
            ? `maps://maps.apple.com/?q=${lat},${lng}`
            : `https://maps.google.com/?q=${lat},${lng}`;

        // Mostrar el SweetAlert2 con opciones
        Swal.fire({
            title: 'Selecciona una opción',
            showDenyButton: true,
            showCancelButton: true,
            confirmButtonText: 'Acercar',
            denyButtonText: `Abrir en Mapas`,
            cancelButtonText: 'Cancelar',
        }).then((result) => {
            if (result.isConfirmed) {
                // Opción "Acercar" seleccionada
                const latLng = new google.maps.LatLng(lat, lng);
                map.setCenter(latLng);
                map.setZoom(17);  // Puedes ajustar el nivel de zoom según tus necesidades

                // Destacar el marcador del inmueble (puedes cambiar su ícono temporalmente o agregar una animación)
                markers.forEach(marker => {
                    if (marker.getPosition().equals(latLng)) {
                        marker.setAnimation(google.maps.Animation.BOUNCE);  // Ejemplo: hacer que el marcador salte
                        setTimeout(() => marker.setAnimation(null), 1400);  // Detener la animación después de un tiempo
                    }
                });
            } else if (result.isDenied) {
                // Opción "Abrir en Mapas" seleccionada
                window.open(url, '_blank');
            } else if (result.isDismissed) { // && map.getZoom() !== 12) {
                // Opción "Cancelar" seleccionada y el zoom es diferente a 12
                map.setCenter(latLng);
                map.setZoom(12);
            }
            $("#modalInmueble").modal("hide");
        });
    } else {
        Swal.fire('Error', 'No hay un inmueble seleccionado.', 'error');
    }
}


let accumulatedUrls = []; // Variable para acumular los enlaces de los inmuebles
let accumulatedIds = []; // Variable para acumular los IDs de los inmuebles

//function fetchMarkers pruebas
function fetchMarkers() {
    fetch("/Home/listaInmuebles")
        .then(response => response.ok ? response.json() : Promise.reject(response))
        .then(responseJson => {
            if (responseJson.length > 0) {
                responseJson.forEach((item) => {
                    let imageIcon;
                    switch (item.idTipo) {
                        case 2: imageIcon = "images/icon/casa_che.png"; typeofp = "Casa en venta"; break;
                        case 3: imageIcon = "images/icon/casa.png"; typeofp = "Casa en renta"; break;
                        case 4: imageIcon = "images/icon/apartment1.png"; typeofp = "Apartamento en venta"; break;
                        case 5: imageIcon = "images/icon/apartment2.png"; typeofp = "Apartamento en renta"; break;
                        case 6: imageIcon = "images/icon/terreno1b.png"; typeofp = "Terreno en venta"; break;
                        case 7: imageIcon = "images/icon/terreno2.png"; typeofp = "Terreno en renta"; break;
                        case 8: imageIcon = "images/icon/local.png"; typeofp = "Local en venta"; break;
                        case 9: imageIcon = "images/icon/local2.png"; typeofp = "Local en renta"; break;
                        case 10: imageIcon = "images/icon/building.png"; typeofp = "Edificio en venta"; break;
                        case 11: imageIcon = "images/icon/building2.png"; typeofp = "Edificio en renta"; break;
                        case 12: imageIcon = "images/icon/montacargas2.png"; typeofp = "Bodega en venta"; break;
                        case 13: imageIcon = "images/icon/montacargas.png"; typeofp = "Bodega en renta"; break;
                        case 14: imageIcon = "images/icon/desk.png"; typeofp = "Oficina en venta"; break;
                        case 15: imageIcon = "images/icon/desk2.png"; typeofp = "Oficina en renta"; break;
                        case 16: imageIcon = "images/icon/tractor1.png"; typeofp = "Rancho en venta"; break;
                        case 17: imageIcon = "images/icon/tractor2.png"; typeofp = "Rancho en renta"; break;
                    }

                    const latLng = new google.maps.LatLng(item.lat, item.lng);
                    const markerx = new google.maps.Marker({
                        position: latLng,
                        map,
                        title: String(item.idTipo),
                        icon: {
                            url: imageIcon,
                            scaledSize: new google.maps.Size(32, 32),
                            origin: new google.maps.Point(0, 0),
                            anchor: new google.maps.Point(16, 16)
                        },
                    });

                    markerx.customInfo = item.precio;
                    markersx[i] = markerx;
                    i++;

                    let longPressDuration = 1000; // Duración del long press en milisegundos (1 segundo)
                    let longPressTimer;
                    let isLongPress = false;

                    const handleLongPress = () => {
                        if (!mousedUp) { // Verifica que el mapa no se esté arrastrando
                            isLongPress = true;
                            const url = `${window.location.origin}${window.location.pathname}?inmuebleId=${item.idInmueble}`;
                            accumulatedUrls.push(url);
                            accumulatedIds.push(item.idInmueble);
                            const accumulatedIdsText = accumulatedIds.join(', ');
                            const accumulatedUrlsText = `Tengo estas propiedades para ti:\n${accumulatedUrls.join('\n')}`;

                            if (navigator.clipboard && navigator.clipboard.writeText) {
                                navigator.clipboard.writeText(accumulatedUrlsText).then(function () {
                                    Swal.fire({
                                        title: `Copiado a memoria: Inmueble #${item.idInmueble}`,
                                        html: `Inmuebles copiados: ${accumulatedIdsText}`,
                                        icon: 'success',
                                        confirmButtonText: 'Ok',
                                        allowOutsideClick: false,
                                        showCancelButton: true,
                                        cancelButtonText: 'Vaciar memoria',
                                        reverseButtons: true, // Esto coloca el botón de cancelar a la izquierda
                                    }).then((result) => {
                                        if (result.dismiss === Swal.DismissReason.cancel) {
                                            accumulatedUrls = [];
                                            accumulatedIds = [];
                                        }
                                    });
                                }, function (err) {
                                    console.error('Error al copiar texto: ', err);
                                });
                            } else {
                                // Fallback para navegadores que no soportan navigator.clipboard.writeText
                                const textArea = document.createElement('textarea');
                                textArea.value = accumulatedUrlsText;
                                document.body.appendChild(textArea);
                                textArea.select();
                                document.execCommand('copy');
                                document.body.removeChild(textArea);
                                Swal.fire({
                                    title: `Copiado a memoria: Inmueble #${item.idInmueble}`,
                                    html: `Inmuebles copiados: ${accumulatedIdsText}`,
                                    icon: 'success',
                                    confirmButtonText: 'Ok',
                                    allowOutsideClick: false,
                                    showCancelButton: true,
                                    cancelButtonText: 'Vaciar memoria'
                                }).then((result) => {
                                    if (result.dismiss === Swal.DismissReason.cancel) {
                                        accumulatedUrls = [];
                                        accumulatedIds = [];
                                    }
                                });
                            }
                        }
                    };

                    const onPressStart = (event) => {
                        isLongPress = false;
                        mousedUp = false; // Resetea mousedUp al iniciar el presionado
                        longPressTimer = setTimeout(handleLongPress, longPressDuration);
                        if (event.type === 'touchstart') {
                            event.preventDefault(); // Previene el comportamiento predeterminado en dispositivos táctiles
                        }
                    };

                    const onPressEnd = () => {
                        clearTimeout(longPressTimer); // Cancelar el temporizador si se suelta antes de long press
                        mousedUp = true; // Restablecer mousedUp al soltar
                        setTimeout(() => { isLongPress = false; }, longPressDuration); // Reset after the click event
                    };

                    const onPressMove = () => {
                        clearTimeout(longPressTimer); // Cancelar el temporizador si el marcador se mueve
                        isLongPress = false;
                        mousedUp = true; // Restablecer mousedUp si se mueve el marcador
                    };

                    markerx.addListener('click', function () {
                        if (!isLongPress && mousedUp) { // Verifica que el mapa no se esté arrastrando y que no sea un long press
                            $('#btnClear').css('display', "none");
                            $('.btn-fileupload').css('display', "none");

                            currentInmueble = {
                                lat: item.lat,
                                lng: item.lng,
                                id: item.idInmueble
                            };

                            const str = document.getElementById("lnkAcceso").innerText;
                            const str2 = item.refUsuario.correo.toString();

                            const res = str.toUpperCase();
                            const res2 = str2.toUpperCase();

                            if (res != res2) {
                                $('.boton-eliminar-inmueble').css('display', "none");
                                $("#contacto_a").hide();
                                $('.boton-guardar-inmueble').css('display', "none");
                            } else {
                                $("#contacto_a").show();
                                $('.boton-guardar-inmueble').css('display', "inline");
                                $(".boton-eliminar-inmueble").show();

                                if (item) {
                                    $('.boton-guardar-inmueble').text('Actualizar');
                                    $('.boton-guardar-inmueble').attr('onclick', 'validateForm(event, true)');
                                    isUpdate = true;
                                } else {
                                    $('.boton-guardar-inmueble').text('Guardar');
                                    $('.boton-guardar-inmueble').attr('onclick', 'validateForm(event, false)');
                                    isUpdate = false;
                                }
                            }

                            selectedInmuebleId = item.idInmueble;
                            clikeado = 1;
                            var nom_tel = item.refUsuario.nombres + " " + item.refUsuario.aPaterno;
                            GetCode1(item.idTipo, item.idInmueble, nom_tel, item.telefono, item.terreno, item.construccion, item.precio, item.observaciones, item.contacto, item.imagenes);
                        }
                    });

                    markerx.addListener('mousedown', onPressStart);
                    markerx.addListener('mouseup', onPressEnd);
                    markerx.addListener('mousemove', onPressMove);
                    markerx.addListener('touchstart', onPressStart);
                    markerx.addListener('touchend', onPressEnd);
                    markerx.addListener('touchmove', onPressMove);
                });
                map.setCenter(latLng);
            }
        })
        .catch(error => {
            console.error('Error al obtener la lista de inmuebles:', error);
        });
}




//Origi Funcionando Ok, selecciona, lleva a memoria, pero está muy sensible cuando selecciona y arrastra, copia a memoria pero no debería hacerlo mientras arrastra

//function fetchMarkers() {
//    fetch("/Home/listaInmuebles")
//        .then(response => response.ok ? response.json() : Promise.reject(response))
//        .then(responseJson => {
//            if (responseJson.length > 0) {
//                responseJson.forEach((item) => {
//                    let imageIcon;
//                    switch (item.idTipo) {
//                        case 2: imageIcon = "images/icon/casa_che.png"; typeofp = "Casa en venta"; break;
//                        case 3: imageIcon = "images/icon/casa.png"; typeofp = "Casa en renta"; break;
//                        case 4: imageIcon = "images/icon/apartment1.png"; typeofp = "Apartamento en venta"; break;
//                        case 5: imageIcon = "images/icon/apartment2.png"; typeofp = "Apartamento en renta"; break;
//                        case 6: imageIcon = "images/icon/terreno1b.png"; typeofp = "Terreno en venta"; break;
//                        case 7: imageIcon = "images/icon/terreno2.png"; typeofp = "Terreno en renta"; break;
//                        case 8: imageIcon = "images/icon/local.png"; typeofp = "Local en venta"; break;
//                        case 9: imageIcon = "images/icon/local2.png"; typeofp = "Local en renta"; break;
//                        case 10: imageIcon = "images/icon/building.png"; typeofp = "Edificio en venta"; break;
//                        case 11: imageIcon = "images/icon/building2.png"; typeofp = "Edificio en renta"; break;
//                        case 12: imageIcon = "images/icon/montacargas2.png"; typeofp = "Bodega en venta"; break;
//                        case 13: imageIcon = "images/icon/montacargas.png"; typeofp = "Bodega en renta"; break;
//                        case 14: imageIcon = "images/icon/desk.png"; typeofp = "Oficina en venta"; break;
//                        case 15: imageIcon = "images/icon/desk2.png"; typeofp = "Oficina en renta"; break;
//                        case 16: imageIcon = "images/icon/tractor1.png"; typeofp = "Rancho en venta"; break;
//                        case 17: imageIcon = "images/icon/tractor2.png"; typeofp = "Rancho en renta"; break;
//                    }

//                    const latLng = new google.maps.LatLng(item.lat, item.lng);
//                    const markerx = new google.maps.Marker({
//                        position: latLng,
//                        map,
//                        title: String(item.idTipo),
//                        icon: {
//                            url: imageIcon,
//                            scaledSize: new google.maps.Size(32, 32),
//                            origin: new google.maps.Point(0, 0),
//                            anchor: new google.maps.Point(16, 16)
//                        },
//                    });

//                    markerx.customInfo = item.precio;
//                    markersx[i] = markerx;
//                    i++;

//                    let longPressDuration = 1000; // Duración del long press en milisegundos (1 segundo)
//                    let longPressTimer;
//                    let isLongPress = false;

//                    const handleLongPress = () => {
//                        isLongPress = true;
//                        const url = `${window.location.origin}${window.location.pathname}?inmuebleId=${item.idInmueble}`;
//                        accumulatedUrls.push(url);
//                        accumulatedIds.push(item.idInmueble);
//                        const accumulatedIdsText = accumulatedIds.join(', ');
//                        const accumulatedUrlsText = `Tengo estas propiedades para ti:\n${accumulatedUrls.join('\n')}`;

//                        if (navigator.clipboard && navigator.clipboard.writeText) {
//                            navigator.clipboard.writeText(accumulatedUrlsText).then(function () {
//                                Swal.fire({
//                                    title: `Copiado a memoria: Inmueble #${item.idInmueble}`,
//                                    html: `Inmuebles copiados: ${accumulatedIdsText}`,
//                                    icon: 'success',
//                                    confirmButtonText: 'Ok',
//                                    allowOutsideClick: false,
//                                    showCancelButton: true,
//                                    cancelButtonText: 'Vaciar memoria',
//                                    reverseButtons: true, // Esto coloca el botón de cancelar a la izquierda
//                                }).then((result) => {
//                                    if (result.dismiss === Swal.DismissReason.cancel) {
//                                        accumulatedUrls = [];
//                                        accumulatedIds = [];
//                                    }
//                                });
//                            }, function (err) {
//                                console.error('Error al copiar texto: ', err);
//                            });
//                        } else {
//                            // Fallback para navegadores que no soportan navigator.clipboard.writeText
//                            const textArea = document.createElement('textarea');
//                            textArea.value = accumulatedUrlsText;
//                            document.body.appendChild(textArea);
//                            textArea.select();
//                            document.execCommand('copy');
//                            document.body.removeChild(textArea);
//                            Swal.fire({
//                                title: `Copiado a memoria: Inmueble #${item.idInmueble}`,
//                                html: `Inmuebles copiados: ${accumulatedIdsText}`,
//                                icon: 'success',
//                                confirmButtonText: 'Ok',
//                                allowOutsideClick: false,
//                                showCancelButton: true,
//                                cancelButtonText: 'Vaciar memoria'
//                            }).then((result) => {
//                                if (result.dismiss === Swal.DismissReason.cancel) {
//                                    accumulatedUrls = [];
//                                    accumulatedIds = [];
//                                }
//                            });
//                        }
//                    };

//                    const onPressStart = (event) => {
//                        isLongPress = false;
//                        longPressTimer = setTimeout(handleLongPress, longPressDuration);
//                        if (event.type === 'touchstart') {
//                            event.preventDefault(); // Previene el comportamiento predeterminado en dispositivos táctiles
//                        }
//                    };

//                    const onPressEnd = () => {
//                        clearTimeout(longPressTimer); // Cancelar el temporizador si se suelta antes de long press
//                        setTimeout(() => { isLongPress = false; }, longPressDuration); // Reset after the click event
//                    };

//                    const onPressMove = () => {
//                        clearTimeout(longPressTimer); // Cancelar el temporizador si el marcador se mueve
//                        isLongPress = false;
//                    };

//                    markerx.addListener('click', function () {
//                        if (!isLongPress) {
//                            $('#btnClear').css('display', "none");
//                            $('.btn-fileupload').css('display', "none");

//                            currentInmueble = {
//                                lat: item.lat,
//                                lng: item.lng,
//                                id: item.idInmueble
//                            };

//                            const str = document.getElementById("lnkAcceso").innerText;
//                            const str2 = item.refUsuario.correo.toString();

//                            const res = str.toUpperCase();
//                            const res2 = str2.toUpperCase();

//                            if (res != res2) {
//                                $('.boton-eliminar-inmueble').css('display', "none");
//                                $("#contacto_a").hide();
//                                $('.boton-guardar-inmueble').css('display', "none");
//                            } else {
//                                $("#contacto_a").show();
//                                $('.boton-guardar-inmueble').css('display', "inline");
//                                $(".boton-eliminar-inmueble").show();

//                                if (item) {
//                                    $('.boton-guardar-inmueble').text('Actualizar');
//                                    $('.boton-guardar-inmueble').attr('onclick', 'validateForm(event, true)');
//                                    isUpdate = true;
//                                } else {
//                                    $('.boton-guardar-inmueble').text('Guardar');
//                                    $('.boton-guardar-inmueble').attr('onclick', 'validateForm(event, false)');
//                                    isUpdate = false;
//                                }
//                            }

//                            selectedInmuebleId = item.idInmueble;
//                            clikeado = 1;
//                            var nom_tel = item.refUsuario.nombres + " " + item.refUsuario.aPaterno;
//                            GetCode1(item.idTipo, item.idInmueble, nom_tel, item.telefono, item.terreno, item.construccion, item.precio, item.observaciones, item.contacto, item.imagenes);
//                        }
//                    });

//                    markerx.addListener('mousedown', onPressStart);
//                    markerx.addListener('mouseup', onPressEnd);
//                    markerx.addListener('mousemove', onPressMove);
//                    markerx.addListener('touchstart', onPressStart);
//                    markerx.addListener('touchend', onPressEnd);
//                    markerx.addListener('touchmove', onPressMove);
//                });
//                map.setCenter(latLng);
//            }
//        })
//        .catch(error => {
//            console.error('Error al obtener la lista de inmuebles:', error);
//        });
//}


document.getElementById('copyIdToClipboard').addEventListener('change', function (event) {
    if (event.target.checked) {
        if (currentInmueble) {
            const url = `${window.location.origin}${window.location.pathname}?inmuebleId=${currentInmueble.id}`;
            accumulatedUrls.push(url);
            accumulatedIds.push(currentInmueble.id);

            const accumulatedUrlsText = `Tengo estas propiedades para ti:\n${accumulatedUrls.join('\n')}`;
            const accumulatedIdsText = `Inmuebles copiados: ${accumulatedIds.join(', ')}`;

            navigator.clipboard.writeText(accumulatedUrlsText).then(function () {
                Swal.fire({
                    title: `ID #${currentInmueble.id} copiado a memoria`,
                    html: accumulatedIdsText,
                    icon: 'success',
                    showCancelButton: true,
                    confirmButtonText: 'Ok',
                    cancelButtonText: 'Vaciar memoria',
                    reverseButtons: true, // Esto coloca el botón de cancelar a la izquierda
                }).then((result) => {
                    if (result.dismiss === Swal.DismissReason.cancel) {
                        accumulatedUrls = [];
                        accumulatedIds = [];
                        // Desmarcar todos los checkboxes
                        document.querySelectorAll('input[type="checkbox"]').forEach(checkbox => checkbox.checked = false);
                        Swal.fire({
                            title: 'Memoria vaciada',
                            icon: 'success',
                            confirmButtonText: 'Ok'
                        });
                    }
                });
            }).catch(function (error) {
                console.error('Error al copiar el ID:', error);
            });
        }
    } else {
        // Remove from memory if checkbox is unchecked
        if (currentInmueble) {
            const index = accumulatedIds.indexOf(currentInmueble.id);
            if (index !== -1) {
                accumulatedIds.splice(index, 1);
                accumulatedUrls.splice(index, 1);
            }

            const accumulatedUrlsText = `Tengo estas propiedades para ti:\n${accumulatedUrls.join('\n')}`;
            const accumulatedIdsText = `Inmuebles copiados: ${accumulatedIds.join(', ')}`;

            navigator.clipboard.writeText(accumulatedUrlsText).then(function () {
                Swal.fire({
                    title: `ID #${currentInmueble.id} removido de memoria`,
                    html: accumulatedIdsText,
                    icon: 'success',
                    showCancelButton: true,
                    confirmButtonText: 'Ok',
                    cancelButtonText: 'Vaciar memoria',
                    reverseButtons: true, // Esto coloca el botón de cancelar a la izquierda
                }).then((result) => {
                    if (result.dismiss === Swal.DismissReason.cancel) {
                        accumulatedUrls = [];
                        accumulatedIds = [];
                        // Desmarcar todos los checkboxes
                        document.querySelectorAll('input[type="checkbox"]').forEach(checkbox => checkbox.checked = false);
                        Swal.fire({
                            title: 'Memoria vaciada',
                            icon: 'success',
                            confirmButtonText: 'Ok'
                        });
                    }
                });
            }).catch(function (error) {
                console.error('Error al copiar el ID:', error);
            });
        }
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



function shareItem() {
    let inmuebleId
    if (clikeado != 1) {
        const queryParams = getQueryParams();
        inmuebleId = queryParams.inmuebleId;

        if (!inmuebleId && selectedInmuebleId) {
            inmuebleId = selectedInmuebleId;
        }
    }
    else {
        inmuebleId = selectedInmuebleId;
    }

    if (inmuebleId) {
        const url = `${window.location.origin}${window.location.pathname}?inmuebleId=${inmuebleId}`;

        if (navigator.share) {
            navigator.share({
                title: 'Compartir Inmueble',
                text: 'Mira este inmueble que encontré:',
                url: url
            }).then(() => {
                console.log('Gracias por compartir.');
            }).catch((error) => {
                console.error('Error al compartir:', error);
            });
        } else {
            copyToClipboard(url);
            alert(`Compartir enlace: ${url}`);
        }
    } else {
        alert("No hay un inmueble seleccionado para compartir.");
    }
}

function getQueryParams() {
    const params = {};
    const queryString = window.location.search.slice(1);
    if (!queryString) {
        return params; // Return an empty object if there are no query parameters
    }

    queryString.split('&').forEach(param => {
        const [key, value] = param.split('=');
        if (key) {
            params[decodeURIComponent(key)] = value ? decodeURIComponent(value) : '';
        }
    });
    return params;
}

function copyToClipboard(text) {
    const textarea = document.createElement('textarea');
    textarea.value = text;
    document.body.appendChild(textarea);
    textarea.select();
    document.execCommand('copy');
    document.body.removeChild(textarea);
    alert('Enlace copiado al portapapeles');
}




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
                    //location.reload();
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

function numberWithOutCommas(numberString) {
    // Reemplaza cualquier carácter que no sea un dígito (0-9) por una cadena vacía
    return numberString.replace(/[^\d]/g, '');
}

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
    $('#imgViewer').css({
        'overflow-x': 'auto',
        'overflow-y': 'hidden',
        'white-space': 'nowrap',
        'height': 'auto',
        'margin-bottom': '2px',
        'scrollbar-width': 'thin'
    });

    $('#imageStrip').css({
        'overflow-x': 'auto',
        'overflow-y': 'hidden',
        'white-space': 'nowrap',
        'height': 'auto', // Ajusta esta altura según tus necesidades
        'scrollbar-width': 'thin'
    });

    const scrollbarStyle = `
        <style>
            #imgViewer::-webkit-scrollbar, #imageStrip::-webkit-scrollbar {
                height: 4px; /* Reduce la altura de la barra de desplazamiento para Chrome y Safari */
            }
            #imgViewer::-webkit-scrollbar-track, #imageStrip::-webkit-scrollbar-track {
                background: #f1f1f1; /* Color del fondo de la barra de desplazamiento */
            }
            #imgViewer::-webkit-scrollbar-thumb, #imageStrip::-webkit-scrollbar-thumb {
                background: #888; /* Color del cursor de la barra de desplazamiento */
                border-radius: 2px; /* Bordes redondeados del cursor */
            }
            #imgViewer::-webkit-scrollbar-thumb:hover, #imageStrip::-webkit-scrollbar-thumb:hover {
                background: #555; /* Color del cursor cuando se pasa el mouse por encima */
            }
        </style>
    `;

    $('head').append(scrollbarStyle);

    if (j != 0) {
        for (var x = 1; x <= j; x++) {
            var imgSrc = `Cargas/${b}_${x}.jpg`;
            var imgElement = `
        <div id="images${x}" class="item" style="display: inline-block; height: 100%;">
            <img src="${imgSrc}" onclick="panel(this);" id="_${b}_${x}" style="margin-left:2px; height:100%; object-fit: contain;">
        </div>`;
            $('#imgViewer').append(imgElement);

            // Añadir la imagen a la tira horizontal con altura del 80% del modal
            var stripItem = `<img src="${imgSrc}" class="d-block" style="height:80vh; object-fit: contain; margin:auto;">`;
            $('#imageStrip').append(stripItem);
        }
    } else {
        $('#imgViewer').append($('<img>', { src: "images/nohouse.jpg", width: '50px', height: '50px' }));
    }

    // Check if the currentInmueble is already in accumulatedIds and set the checkbox accordingly
    const checkbox = document.getElementById('copyIdToClipboard');
    checkbox.checked = accumulatedIds.includes(b);

    $("#modalInmueble").modal("show");
}



function GetCode2() {
    //$("#txtNombreCompleto").val(_modeloEmpleado.nombreCompleto);
    //$("#cboDepartamento").val(_modeloEmpleado.idDepartamento == 0 ? $("#cboDepartamento option:first").val() : _modeloEmpleado.idDepartamento)
    //$("#txtSueldo").val(_modeloEmpleado.sueldo);
    //$("#txtFechaContrato").val(_modeloEmpleado.fechaContrato)
    $("#modalInmueble").modal("show");
}

let selectedFiles = []; // Variable global para mantener el estado de las imágenes seleccionadas

// Nueva función para actualizar el input file y el contenedor de imágenes de vista previa
function updateFileInputAndPreview() {
    const dataTransfer = new DataTransfer();
    selectedFiles.forEach(file => {
        dataTransfer.items.add(file);
    });

    // Reemplazar los archivos en el input file con los archivos seleccionados
    document.getElementById("FileUpload1").files = dataTransfer.files;

    // Actualizar la vista previa
    const imgViewer = document.getElementById('imgViewer');
    imgViewer.innerHTML = '';
    selectedFiles.forEach((file, index) => {
        const reader = new FileReader();
        reader.onload = function (e) {
            const imgContainer = document.createElement('div');
            imgContainer.style.display = 'inline-block';
            imgContainer.style.position = 'relative';

            const imgElement = document.createElement("img");
            imgElement.src = e.target.result;
            imgElement.style.height = "100px";
            imgElement.style.marginBottom = "2px";
            imgElement.style.marginRight = "2px";
            imgElement.style.display = "inline-block";

            const deleteButton = document.createElement('button');
            deleteButton.textContent = 'X';
            deleteButton.style.position = 'absolute';
            deleteButton.style.top = '0';
            deleteButton.style.right = '0';
            deleteButton.style.backgroundColor = 'red';
            deleteButton.style.color = 'white';
            deleteButton.style.border = 'none';
            deleteButton.style.borderRadius = '50%';
            deleteButton.style.cursor = 'pointer';
            deleteButton.addEventListener('click', function () {
                selectedFiles.splice(index, 1); // Eliminar el archivo de la lista
                updateFileInputAndPreview(); // Actualizar la vista previa y el input file
            });

            imgContainer.appendChild(imgElement);
            imgContainer.appendChild(deleteButton);
            imgViewer.appendChild(imgContainer);
        };
        reader.readAsDataURL(file);
    });
}

// Modificar la función ShowImagePreview para actualizar la lista de archivos seleccionados
function ShowImagePreview(evt) {
    const files = evt.files;
    if (files.length) {
        const filePromises = [];
        for (let i = 0, f; f = files[i]; i++) {
            if (f.type.startsWith('image/')) {
                filePromises.push(resizeImageFile(f));
            } else {
                console.warn(`File ${f.name} is not an image and will be discarded.`);
            }
        }

        Promise.all(filePromises).then(function (resizedFiles) {
            selectedFiles = [...selectedFiles, ...resizedFiles]; // Agregar archivos redimensionados a la lista de archivos seleccionados
            updateFileInputAndPreview(); // Actualizar la vista previa y el input file
        });
    } else {
        alert("Failed to load files");
    }
}

async function resizeImageFile(file) {
    return new Promise(function (resolve) {
        const reader = new FileReader();
        reader.onload = function (e) {
            const img = new Image();
            img.onload = function () {
                const maxWidth = 1000;
                const maxHeight = 1000;
                let width = img.width;
                let height = img.height;

                if (width > maxWidth || height > maxHeight) {
                    if (width > height) {
                        height *= maxWidth / width;
                        width = maxWidth;
                    } else {
                        width *= maxHeight / height;
                        height = maxHeight;
                    }
                }

                const canvas = document.createElement('canvas');
                canvas.width = width;
                canvas.height = height;
                const ctx = canvas.getContext('2d');
                ctx.drawImage(img, 0, 0, width, height);

                const dataUri = canvas.toDataURL('image/jpeg', 0.7);

                fetch(dataUri).then(res => res.blob()).then(function (blob) {
                    const resizedFile = new File([blob], file.name, { type: 'image/jpeg' });
                    resolve(resizedFile);
                });
            };
            img.src = e.target.result;
        };
        reader.readAsDataURL(file);
    });
}

// Nueva función para limpiar el estado de las imágenes seleccionadas
function clearSelectedFiles() {
    selectedFiles = [];
    updateFileInputAndPreview();
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

// Limpia el modal cuando se cierra //Aquí pudiera hacerse un Zoom para cuando se cirre el modal o se presione el botón "Close" del modalInmueble haga un acercamiento al icono
document.getElementById('modalInmueble').addEventListener('hidden.bs.modal', function () {
    clearPreviewAndFields(); clearAll(); clearSelectedFiles();
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
        submitForm(isUpdate);
    }
}

function submitForm(isUpdate = false) {
    const formData = new FormData();

    const ubicacionTexto = document.getElementById('ubicacion').textContent;
    const { lat, lng } = extractLatLon(ubicacionTexto);

    if (!isUpdate) {
        formData.append('Datax.Lat', lat.toFixed(6));
        formData.append('Datax.Lng', lng.toFixed(6));

    }
    else {
        formData.append('idInmueble', selectedInmuebleId);
    }

    formData.append('Datax.IdTipo', $("#tipo").val());
    formData.append('Datax.Terreno', $("#terreno").val());
    formData.append('Datax.Construccion', $("#construccion").val());

    const prePrecio = document.getElementById("precio").value;
    const precio = numberWithOutCommas(prePrecio); // Eliminando comas

    formData.append('Datax.Precio', precio); // Usando el precio sin comas

    formData.append('Datax.Precio', $("#precio").val());
    formData.append('Datax.Observaciones', $("#descripcion").val());
    formData.append('Datax.Contacto', $("#contacto_a").val());

    if (!isUpdate) {
        const files = document.getElementById("FileUpload1").files;
        for (let i = 0; i < files.length; i++) {
            formData.append('Files', files[i]);
        }
    }

    formData.append('Correo', document.getElementById("lnkAcceso").innerText);

    const url = isUpdate ? "/Inmueble/ActualizarInmueble" : "/Inmueble/RegistrarInmueble";
    fetch(url, {
        method: 'POST',
        body: formData
    })
        .then(response => response.json())
        .then(response => {
            if (response.success) {
                window.history.replaceState({}, document.title, window.location.pathname);
                selectedInmuebleId = 0;
                location.reload();
            } else {
                alert("Error al " + (isUpdate ? "actualizar" : "guardar") + " el inmueble y las imágenes");
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


$(document).on("click", "#cerrarmodalImagenCompleta", function () {
    $("#modalImagenCompleta").modal("hide");
});

$(document).on("click", ".boton-eliminar-inmueble", function () {
    if (currentInmuebleId) {
        Swal.fire({
            title: '¿Estás seguro?',
            text: "¿Deseas eliminar el inmueble con ID #" + currentInmuebleId + "?",
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#3085d6',
            cancelButtonColor: '#d33',
            confirmButtonText: 'Sí, eliminarlo!',
            cancelButtonText: 'No, cancelar'
        }).then((result) => {
            if (result.isConfirmed) {
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
                            Swal.fire("Eliminado!", "El inmueble ha sido eliminado", "success").then(() => {
                                location.reload();
                            });
                        } else {
                            Swal.fire("Error", "Error al eliminar el inmueble", "error");
                        }
                    })
                    .catch(error => {
                        console.error('Error al eliminar el inmueble:', error);
                        Swal.fire("Error", "Error en la red o servidor", "error");
                    });
            }
        });
    } else {
        Swal.fire("Atención", "No se ha seleccionado ningún inmueble para eliminar", "warning");
    }
});



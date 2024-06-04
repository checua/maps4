@{
    ViewData["Title"] = "Map Example";
}

< !DOCTYPE html >
    <html>
        <head>
            <title>@ViewData["Title"]</title>
            <script src="https://maps.googleapis.com/maps/api/js?key=YOUR_API_KEY"></script>
            <script src="https://code.jquery.com/jquery-3.6.0.min.js"></script>
        </head>
        <body>
            <div id="map_canvas" style="width: 100%; height: 600px;"></div>
            <input type="file" id="FileUpload1" multiple />
            <input type="hidden" id="hfCoordenadas" />
            <input type="hidden" id="hfLat" />
            <input type="hidden" id="hfLng" />
            <input type="hidden" id="hfDireccion" />
            <input type="hidden" id="hfidInmueble" />
            <input type="hidden" id="hfCorreo" value="your-email@example.com" />
            <button id="btnEliminar" style="display: none;">Eliminar</button>
            <div id="lnkNombre">No logged</div>

            <script>
                window.onload = function () {
            var input2 = document.getElementById('FileUpload1');
                input2.addEventListener("change", readMultipleFiles, false);

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

                fetch('/Map/GetInmJson', {
                    method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                }
            })
            .then(response => response.json())
            .then(data => {
                    OnSuccessCall(data);
            })
            .catch(error => {
                    OnErrorCall(error);
            });

                google.maps.event.addListener(map, 'dblclick', function (event) { });

                google.maps.event.addListener(map, 'mousedown', function (event) {
                    mousedUp = false;
                setTimeout(function () {
                    if (mousedUp === false) {
                        var log = document.getElementById("lnkNombre").innerText;

                if (log == "No logged")
                alert("Ingresa para poder registrar propiedades");
                else {
                    geocoder.geocode({ 'latLng': event.latLng }, function (results, status) {
                        if (status == google.maps.GeocoderStatus.OK) {
                            if (results[0]) {
                                document.getElementById("hfCoordenadas").value = results[0].geometry.location;
                                document.getElementById("hfLat").value = event.latLng.lat().toFixed(6);
                                document.getElementById("hfLng").value = event.latLng.lng().toFixed(6);
                                document.getElementById("hfDireccion").value = results[0].formatted_address;

                                marker = new google.maps.Marker({
                                    position: event.latLng,
                                    map: map
                                });

                                GetCode2();
                            } else {
                                document.getElementById('geocoding').innerHTML = 'No se encontraron resultados';
                            }
                        } else {
                            document.getElementById('geocoding').innerHTML = 'Geocodificación ha fallado debido a: ' + status;
                        }
                    });
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

                google.maps.event.addListener(infowindow, 'click', function (event) {
                    alert("hola");
            });

                function OnSuccessCall(response) {
                var markerData = JSON.parse(response.d);
                var i = 0;
                var a1 = new Array();

                var latLng;

                markerData.Table.forEach(function (val) {
                    var imageIcon;
                switch (val.idTipo) {
                        case 0:
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
                break;
                case 7:
                imageIcon = "images/icon/local.png";
                break;
                case 8:
                imageIcon = "images/icon/local2.png";
                break;
                case 9:
                imageIcon = "images/icon/building.png";
                break;
                case 10:
                imageIcon = "images/icon/building2.png";
                break;
                case 11:
                imageIcon = "images/icon/montacargas2.png";
                break;
                case 12:
                imageIcon = "images/icon/montacargas.png";
                break;
                case 13:
                imageIcon = "images/icon/desk.png";
                break;
                case 14:
                imageIcon = "images/icon/desk2.png";
                break;
                case 15:
                imageIcon = "images/icon/tractor2.png";
                break;
                case 16:
                imageIcon = "images/icon/tractor3.png";
                break;
                    }

                latLng = new google.maps.LatLng(val.lat, val.lng);
                var marker = new google.maps.Marker({
                    position: latLng,
                title: String(val.idTipo),
                icon: {
                    url: imageIcon,
                scaledSize: new google.maps.Size(32, 32)
                        },
                map: map
                    });

                marker.customInfo = val.precio;

                markers[i] = marker;
                i++;

                var content = `
                <div id="iw-container">
                    <div id="iw-title">${val.nombreTipo}</div>
                    <div id="iw-laterales">
                        <div id="iw-lateral-izquierda">
                            <a title="fotos" href="${val.link}" target="blank"><img src="images/Collage-Casas.jpg" alt="Imágenes" /></a>
                        </div>
                        <div id="iw-lateral-derecha">
                            <p>${val.observaciones}</p>
                        </div>
                    </div>
                    <div id="iw-abajo">
                        <div class="iw-subTitle2">Asesor: <b>${val.nombreCompleto}</b></div>
                        <div class="iw-subTitle2">Teléfono: <b>${val.telefono}</b></div>
                        <div class="iw-subTitle2">Terreno: <b>${val.terreno}</b>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;Construcción: <b>${val.construccion}</b></div>
                        <div class="iw-subTitle2">Precio: <b>${val.precio}</b></div>
                    </div>
                </div>`;

                var infowindow = new google.maps.InfoWindow({
                    content: content
                    });

                marker.addListener('click', function () {
                    document.getElementById("hfidInmueble").value = val.idInmueble;

                var str = String(document.getElementById("hfCorreo").value).toUpperCase();
                var str2 = String(val.correo).toUpperCase();
                if (str === str2) {
                    document.getElementById('btnEliminar').style.display = 'block';
                        } else {
                    document.getElementById('btnEliminar').style.display = 'none';
                        }

                infowindow.open(map, marker);
                    });
                });
            }

                function OnErrorCall(response) {
                    alert("Falla en la conexión con el servidor");
            }
        };
            </script>
        </body>
    </html>

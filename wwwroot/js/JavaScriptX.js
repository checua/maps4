var map;
var geocoder;
var infoWindow;
var marker;
var markers = [];
var banSiExc = 1;
var banNoExc = 1;
var banCasaVenta = 1;
var banCasaRenta = 1;
var banTerreVenta = 1;
var mousedUp = false;
var degrees = 90;
var img = null;
var canvas = null;

window.onload = function () {
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
}
$(document).ready(function (e) {

    var headerheight = $('#headerTop').height();
    var hheight = $(window).height();
    $('#map_canvas').css('height', hheight); // - headerheight);

    $(window).resize(function () {
        var headerheight = $('#headerTop').height();
        var hheight = $(window).height();
        $('#map_canvas').css('height', hheight); // - headerheight);
    });

    function numberWithCommas(x) {
        return x.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ",");
    }

    $("#terreno").keyup(function () {
        if (event.which >= 37 && event.which <= 40) {
            event.preventDefault();
        }
        $(this).val(function (index, value) {
            value = value.replace(/,/g, '');
            return numberWithCommas(value);
        });
    });
    $("#construccion").keyup(function () {
        if (event.which >= 37 && event.which <= 40) {
            event.preventDefault();
        }
        $(this).val(function (index, value) {
            value = value.replace(/,/g, '');
            return numberWithCommas(value);
        });
    });
    $("#precio").keyup(function () {
        if (event.which >= 37 && event.which <= 40) {
            event.preventDefault();
        }
        $(this).val(function (index, value) {
            value = value.replace(/,/g, '');
            return numberWithCommas(value);
        });
    });

    document.getElementById('contacto_a').addEventListener('keypress', function (e) {
        // Utiliza la expresión regular para permitir solo números
        if (!/^\d+$/.test(e.key) && e.key !== 'Backspace') {
            e.preventDefault();  // Previene la acción por defecto si no es un número
        }
    });
});



function scheduleA(event) {
    showMan();
}

function scheduleB(event) {
    showMan();
}

function showMan() {

    //alert("This is showMan");

    var e = document.getElementById("cboTipoPropiedad");
    var strUser = e.options[e.selectedIndex].value;

    //alert(strUser);

    switch (strUser) {
        //case "0":
        //    Todos();
        //    break;
        case "1":
            Todos();
            break;
        case "2":
            jsMov(2);
            break;
        case "3":
            jsMov(3);
            break;
        case "4":
            jsMov(4);
            break;
        case "5":
            jsMov(5);
            break;
        case "6":
            jsMov(6);
            break;
        case "7":
            jsMov(7);
            break
        case "8":
            jsMov(8);
            break
        case "9":
            jsMov(9);
            break
        case "10":
            jsMov(10);
            break
        case "11":
            jsMov(11);
            break
        case "12":
            jsMov(12);
            break
        case "13":
            jsMov(13);
            break
        case "14":
            jsMov(14);
            break
        case "15":
            jsMov(15);
            break
        case "16":
            jsMov(16);
            break
        case "17":
            jsMov(17);
            break
    }
}

function Todos() {
    //alert("Presionado TODOS en Venta desde proc.");
    var test = $("#ddlViewBy").val().toString();
    test = test.replace(/,/g, '');
    var test2 = parseInt(test);
    //alert(test2);

    var test3 = $("#ddlViewBy2").val().toString();
    test3 = test3.replace(/,/g, '');
    var test4 = parseInt(test3);
    //alert(test4);

    var e = document.getElementById("cboTipoPropiedad");
    var strUser = e.options[e.selectedIndex].value;

    /*alert(strUser);*/

    //alert(markersx.length);

    for (i = 0; i < markersx.length; i++) {

        //alert(markersx[i].title);

        if (markersx[i].customInfo >= test2 && markersx[i].customInfo <= test4) {
            markersx[i].setVisible(true);
            //alert("Visible");
        }
        else {
            markersx[i].setVisible(false);
            //alert("No visible");
        }
            
    }

}

function jsMov(x) {

    var test = $("#ddlViewBy").val().toString();
    test = test.replace(/,/g, '');
    var test2 = parseInt(test);
    //alert(test2);

    var test3 = $("#ddlViewBy2").val().toString();
    test3 = test3.replace(/,/g, '');
    var test4 = parseInt(test3);
    //alert(test4);

    for (i = 0; i < markersx.length; i++) {
        //alert(markersx[i].title);
        //alert(x);

        if (markersx[i].title == x && markersx[i].customInfo >= test2 && markersx[i].customInfo <= test4)
            markersx[i].setVisible(true);
        else
            markersx[i].setVisible(false);
    }
}

function ayantoggle() {
    $(".forgot").slideToggle('slow');
}
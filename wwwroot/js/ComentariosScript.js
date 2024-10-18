document.addEventListener('DOMContentLoaded', function () {


    cargarComentarios();
    cargarTipoPropiedades();

    const comentarioForm = document.getElementById("comentarioForm");

    if (comentarioForm) {
        comentarioForm.addEventListener("submit", async function (event) {
            event.preventDefault();

            const comentarioTexto = document.getElementById("comentario").value;
            const nivel = document.getElementById("nivel").value;
            const tipoInmueble = document.getElementById("tipoInmueble").value;
            const mensajeResultado = document.getElementById("mensaje-resultado");

            // Validar que todos los campos están completos
            if (!comentarioTexto || !nivel || !tipoInmueble) {
                console.error("Los campos Comentario, Nivel y Tipo de Inmueble son obligatorios");
                mensajeResultado.innerText = "Los campos Comentario, Nivel y Tipo de Inmueble son obligatorios";
                mensajeResultado.style.display = "block";
                mensajeResultado.style.color = "red";
                return;
            }

            // Validar si el tipo de inmueble es "Todas" o tiene el valor 1
            if (tipoInmueble === "1" || tipoInmueble === "Todas") {
                mensajeResultado.innerText = "Debe seleccionar un tipo de inmueble válido";
                mensajeResultado.style.display = "block";
                mensajeResultado.style.color = "red";
                return;
            }

            const formData = new FormData();
            formData.set('comentarioTexto', comentarioTexto);
            formData.set('nivel', nivel);
            formData.set('correo', ""); // Se obtiene en el backend
            formData.set('nombre', ""); // Se obtiene en el backend
            formData.set('telefono', ""); // Se obtiene en el backend
            formData.set('fechaComentario', "");
            formData.set('fechaExpiracion', "");
            formData.set('activo', true);
            formData.set('TipoInmuebleSolicitado', tipoInmueble);




            const url = "Comentarios/RegistrarComentario";
            try {
                const response = await fetch(url, {
                    method: 'POST',
                    body: formData
                });

                const responseData = await response.json();
                if (response.ok && responseData.success) {
                    document.getElementById("mensaje-resultado").innerText = "Comentario registrado con éxito";
                    document.getElementById("mensaje-resultado").style.display = "block";
                    await cargarComentarios();
                    comentarioForm.reset();
                } else {
                    document.getElementById("mensaje-resultado").innerText = "Error al registrar el comentario";
                    document.getElementById("mensaje-resultado").style.display = "block";
                }
            } catch (error) {
                console.error('Error al enviar datos:', error);
            }
        });
    }


});



async function cargarComentarios() {
    try {
        const response = await fetch("Comentarios/GetComentariosActivos");
        if (!response.ok) {
            throw new Error("Error al cargar los comentarios");
        }

        const comentarios = await response.json();
        const container = document.getElementById("comentarios-container");
        container.innerHTML = ""; // Limpiar el contenedor antes de cargar los nuevos comentarios

        comentarios.forEach(comentario => {
            const comentarioDiv = document.createElement("div");

            // Asigna la clase según el nivel del comentario
            comentarioDiv.className = `comentario ${comentario.nivel.toLowerCase()}`;

            // Agregar el contenido del comentario con el nivel, nombre, teléfono y texto del comentario
            comentarioDiv.innerHTML = `
                <p><strong>${comentario.nivel}:</strong> ${comentario.comentarioTexto}</p>
                <p><strong>Nombre:</strong> ${comentario.nombre}<br>
                <strong>Teléfono:</strong> ${comentario.telefono}</p>
            `;

            // Agregar el comentario al contenedor
            container.appendChild(comentarioDiv);
        });
    } catch (error) {
        console.error("Error al cargar los comentarios:", error);
    }
}



function cargarTipoPropiedades() {
    fetch("/Home/listaTipoPropiedades")
        .then(response => response.json())
        .then(data => {
            const tipoInmuebleDropdown = document.getElementById("tipoInmueble");

            data.forEach(item => {
                const option = document.createElement("option");
                option.value = item.idTipoPropiedad;
                option.text = item.nombre;
                tipoInmuebleDropdown.appendChild(option);
            });
        })
        .catch(error => console.error('Error al cargar los tipos de propiedades:', error));
}
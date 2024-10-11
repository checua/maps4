document.addEventListener("DOMContentLoaded", function () {
    cargarComentarios();

    const comentarioForm = document.getElementById("comentarioForm");
    if (comentarioForm) {

        const formData = new FormData();
        comentarioForm.addEventListener("submit", async function (event) {
            event.preventDefault();

            const comentarioTexto = document.getElementById("comentario").value;
            const nivel = document.getElementById("nivel").value;

            if (!comentarioTexto || !nivel) {
                console.error("Los campos Comentario y Nivel son obligatorios");
                return;
            }

            formData.append('comentarioTexto', document.getElementById("comentario").value);
            formData.append('nivel', document.getElementById("nivel").value);
            formData.append('correo', "");
            formData.append('nombre', "");
            formData.append('telefono', "");
            formData.append('fechaComentario', "");
            formData.append('fechaExpiracion', "");
            formData.append('activo', true);

            const url = "Comentarios/RegistrarComentario";
            fetch(url, {
                method: 'POST',
                body: formData
            })
                .then(response => response.json())
                .then(response => {
                    if (response.success) {
                        //alert("Success");
                    } else {
                        //alert("Error");
                    }
                })
                .catch(error => {
                    console.error('Error al enviar datos:', error);
                    alert("Error en la red o servidor");
                });

        });
    } else {
        console.error("El formulario de comentario no se encuentra en el DOM.");
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
        container.innerHTML = "";

        comentarios.forEach(comentario => {
            const comentarioDiv = document.createElement("div");
            comentarioDiv.className = "comentario";
            comentarioDiv.innerHTML = `<p><strong>${comentario.nivel}:</strong> ${comentario.comentarioTexto}</p>`;
            container.appendChild(comentarioDiv);
        });
    } catch (error) {
        console.error(error);
    }
}
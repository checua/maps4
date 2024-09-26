'use strict';

const express = require('express');
const fetch = require('node-fetch'); // Asegúrate de instalar `node-fetch` con npm
const app = express();
const port = 3000; // Puedes cambiar el puerto si lo deseas

// Ruta para generar la página con metadatos Open Graph
app.get('/share', async (req, res) => {
    const inmuebleId = req.query.inmuebleId;

    if (!inmuebleId) {
        return res.status(400).send('Se requiere el ID del inmueble');
    }

    try {
        // Obtener los datos del inmueble según el ID
        const inmuebleData = await loadInmuebleData(inmuebleId);

        if (!inmuebleData) {
            return res.status(404).send('Inmueble no encontrado');
        }

        // Generar el HTML con los metadatos Open Graph
        const html = `
            <!DOCTYPE html>
            <html lang="es">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                
                <!-- Metadatos Open Graph -->
                <meta property="og:title" content="${inmuebleData.titulo}">
                <meta property="og:description" content="${inmuebleData.descripcion}">
                <meta property="og:image" content="https://rsmap.azurewebsites.net/Cargas/${inmuebleData.id}_1.jpg">
                <meta property="og:url" content="https://rsmap.azurewebsites.net/share?inmuebleId=${inmuebleData.id}">
                <meta property="og:type" content="website">
                
                <title>${inmuebleData.titulo}</title>
            </head>
            <body>
                <h1>${inmuebleData.titulo}</h1>
                <p>${inmuebleData.descripcion}</p>
                <img src="https://rsmap.azurewebsites.net/Cargas/${inmuebleData.id}_1.jpg" alt="Vista previa del inmueble">
                <!-- Otros detalles del inmueble -->
            </body>
            </html>
        `;

        res.send(html);
    } catch (error) {
        console.error('Error al generar la página:', error);
        res.status(500).send('Error interno del servidor');
    }
});

// Esta función debería obtener los datos reales del inmueble
async function loadInmuebleData(inmuebleId) {
    try {
        const response = await fetch(`https://rsmap.azurewebsites.net/Inmueble/GetInmuebleById?id=${inmuebleId}`);
        const inmueble = await response.json();

        if (inmueble && inmueble.length > 0) {
            // Retornar el primer elemento del array con sus detalles
            return {
                id: inmueble[0].idInmueble,
                titulo: `Inmueble ${inmueble[0].idInmueble}`,
                descripcion: inmueble[0].observaciones || "Descripción no disponible",
                lat: inmueble[0].lat,
                lng: inmueble[0].lng,
                refUsuario: inmueble[0].refUsuario,
                telefono: inmueble[0].telefono,
                terreno: inmueble[0].terreno,
                construccion: inmueble[0].construccion,
                precio: inmueble[0].precio,
                imagenes: inmueble[0].imagenes
            };
        } else {
            console.warn(`Inmueble ${inmuebleId} no encontrado.`);
            return null; // Retornar null si el inmueble no fue encontrado
        }
    } catch (error) {
        console.error('Error al cargar el inmueble:', error);
        return null; // Retornar null en caso de error
    }
}

app.listen(port, () => {
    console.log(`Servidor de Open Graph corriendo en http://localhost:${port}`);
});

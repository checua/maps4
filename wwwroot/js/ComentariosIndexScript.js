let isScrollingManually = false;
const cortinaAnuncios = document.getElementById('cortina-anuncios');
const anunciosScrollContainer = document.getElementById('anuncios-scroll-container');
let isPaused = false; // Para pausar el desplazamiento

// Función para cargar comentarios desde el servidor
async function cargarComentarios() {
    try {
        const response = await fetch("Comentarios/GetComentariosActivos");
        if (!response.ok) {
            throw new Error("Error al cargar los comentarios");
        }

        const comentarios = await response.json();
        const container = document.getElementById("cortina-anuncios");
        container.innerHTML = ""; // Limpiar contenedor antes de cargar los nuevos comentarios

        comentarios.forEach(comentario => {
            const comentarioDiv = document.createElement("div");
            comentarioDiv.className = `comentario ${comentario.nivel.toLowerCase()}`; // Asigna clase según nivel
            comentarioDiv.innerHTML = `<p><strong>${comentario.nivel}:</strong> ${comentario.comentarioTexto}</p>`;
            container.appendChild(comentarioDiv);
        });

        // Iniciar animación de desplazamiento
        startScrollAnimation();

    } catch (error) {
        console.error("Error al cargar los comentarios:", error);
    }
}

// Inicia la animación de desplazamiento hacia arriba
function startScrollAnimation() {
    const cortinaAnuncios = document.getElementById('cortina-anuncios');
    cortinaAnuncios.style.animationPlayState = 'running';
    cortinaAnuncios.style.animation = 'scrollAnuncios 30s linear infinite'; // Ajusta la velocidad de desplazamiento
}

// Pausa y reanuda la animación al hacer clic
function toggleScroll() {
    const cortinaAnuncios = document.getElementById('cortina-anuncios');
    isPaused = !isPaused;
    cortinaAnuncios.style.animationPlayState = isPaused ? 'paused' : 'running';
}

// Escuchar clics en los comentarios para pausar/reanudar
document.getElementById('cortina-anuncios').addEventListener('click', toggleScroll);

// Detectar desplazamiento manual
anunciosScrollContainer.addEventListener('scroll', () => {
    isScrollingManually = true;
    cortinaAnuncios.style.animationPlayState = 'paused';

    // Detiene el desplazamiento automático mientras el usuario está interactuando
    clearTimeout(anunciosScrollContainer.autoScrollTimeout);
    anunciosScrollContainer.autoScrollTimeout = setTimeout(() => {
        isScrollingManually = false;
        cortinaAnuncios.style.animationPlayState = 'running';
    }, 2000); // Reanudar después de 2 segundos sin interacción
});

//document.querySelectorAll('#cortina-anuncios p.solicitar').forEach((element, index) => {
//    // Alterna colores según el índice
//    if (index % 2 === 0) {
//        element.style.backgroundColor = '#d1ecf1'; // Azul claro
//        element.style.borderLeft = '5px solid #17a2b8';
//    } else {
//        element.style.backgroundColor = '#f8d7da'; // Rojo claro
//        element.style.borderLeft = '5px solid #dc3545';
//    }
//});

document.addEventListener('DOMContentLoaded', () => {

    cargarComentarios();
    setInterval(cargarComentarios, 30000); // Recarga los comentarios cada 30 segundos


    const savedSpeed = localStorage.getItem('scrollSpeed');
    if (savedSpeed) {
        currentSpeed = parseInt(savedSpeed, 10);
        setAnimationSpeed(currentSpeed);
    } else {
        setAnimationSpeed(currentSpeed); // Usa la velocidad predeterminada
    }

    // Ajustar el comportamiento de la marquesina para cambiar colores entre anuncios
    const anuncios = document.querySelectorAll('#cortina-anuncios p');
    anuncios.forEach((anuncio, index) => {
        anuncio.style.backgroundColor = index % 2 === 0 ? '#f8d7da' : '#fff3cd';
        anuncio.style.borderLeft = '5px solid ' + (index % 2 === 0 ? '#dc3545' : '#ffc107');
    });
});

//function toggleMarquesina() {
//    const cortinaAnunciosContainer = document.getElementById('cortina-anuncios-container');

//    // Mostrar u ocultar la marquesina
//    cortinaAnunciosContainer.style.display = cortinaAnunciosContainer.style.display === 'none' ? 'block' : 'none';

//    // Ocultar el menú flotante
//    $("#menu-flotante").hide();
//}

let isMarquesinaVisible = false; // Estado de visibilidad de la marquesina
let currentSpeed = 20; // Velocidad predeterminada
//let isPaused = false; // Estado para pausar la marquesina

// Función para mostrar/ocultar la marquesina
function toggleMarquesina() {
    const cortinaAnunciosContainer = document.getElementById('cortina-anuncios-container');
    const pestanaAnuncios = document.getElementById('pestana-anuncios');
    const menuFlotante = document.getElementById('menu-flotante');

    if (isMarquesinaVisible) {
        cortinaAnunciosContainer.style.right = '-300px'; // Ocultar marquesina
        pestanaAnuncios.style.right = '0'; // Regresar pestaña al borde derecho de la pantalla
        pestanaAnuncios.classList.remove('expanded');
        pestanaAnuncios.classList.add('collapsed');
    } else {
        cortinaAnunciosContainer.style.right = '0'; // Mostrar marquesina
        pestanaAnuncios.style.right = '300px'; // Mover la pestaña al borde izquierdo de la marquesina
        pestanaAnuncios.classList.remove('collapsed');
        pestanaAnuncios.classList.add('expanded');
    }

    // Desaparecer menú flotante al mostrar la marquesina
    if (menuFlotante) {
        menuFlotante.style.display = 'none';
    }

    isMarquesinaVisible = !isMarquesinaVisible;
}

// Función para ajustar la velocidad de la animación
function setAnimationSpeed(speed) {
    cortinaAnuncios.style.animationDuration = `${speed}s`; // Cambia la duración de la animación
    localStorage.setItem('scrollSpeed', speed); // Guarda la velocidad en localStorage
}

// Función para cambiar la velocidad
function cambiarVelocidad(accion, event) {
    event.stopPropagation(); // Evitar que se pause al hacer clic en las flechas

    if (accion === 'aumentar' && currentSpeed > 5) {
        currentSpeed -= 5; // Aumenta la velocidad reduciendo el tiempo
        isPaused = false;  // Reanuda el movimiento si estaba pausado
    } else if (accion === 'disminuir') {
        if (currentSpeed < 60) {
            currentSpeed += 5; // Disminuye la velocidad aumentando el tiempo
        }
        if (currentSpeed >= 60) {
            isPaused = true;  // Pausa el movimiento cuando la velocidad es máxima
        }
    }

    cortinaAnuncios.style.animationPlayState = isPaused ? 'paused' : 'running'; // Pausar si la velocidad es máxima
    setAnimationSpeed(currentSpeed);
}




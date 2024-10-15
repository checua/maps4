﻿function toggleMarquesina() {
    const cortinaAnunciosContainer = document.getElementById('cortina-anuncios-container');

    // Mostrar u ocultar la marquesina
    cortinaAnunciosContainer.style.display = cortinaAnunciosContainer.style.display === 'none' ? 'block' : 'none';

    // Ocultar el menú flotante
    $("#menu-flotante").hide();
}

let isMarquesinaVisible = false; // Estado de visibilidad de la marquesina
let currentSpeed = 20; // Velocidad predeterminada
let isPaused = false; // Estado para pausar la marquesina

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

// Al cargar la página, leer la velocidad guardada
document.addEventListener('DOMContentLoaded', () => {
    const savedSpeed = localStorage.getItem('scrollSpeed');
    if (savedSpeed) {
        currentSpeed = parseInt(savedSpeed, 10);
        setAnimationSpeed(currentSpeed);
    } else {
        setAnimationSpeed(currentSpeed); // Usa la velocidad predeterminada
    }
});


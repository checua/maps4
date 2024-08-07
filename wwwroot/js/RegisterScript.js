$(document).ready(function () {
    function validateForm() {
        const password = $("#password").val();
        const confirmPassword = $("#confirmPassword").val();
        let email = $("#email").val().trim(); // Eliminar espacios en blanco al principio y al final
        const emailPattern = /^[a-zA-Z0-9._-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,6}$/;
        let isValid = true;
        let errorMessage = "";

        if (password !== confirmPassword) {
            isValid = false;
            errorMessage += "Las contraseñas no coinciden.<br>";
        }

        if (!emailPattern.test(email)) {
            isValid = false;
            errorMessage += "El correo no es válido.<br>";
        }

        $("#error-message").html(errorMessage);
        $("#registerBtn").prop("disabled", !isValid);
    }

    $("#password, #confirmPassword, #email").on("keyup", validateForm);
});
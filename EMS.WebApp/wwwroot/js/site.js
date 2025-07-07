/**
 * Bootstrap 5 alert
 * @param {string} type    – one of 'success', 'danger', 'warning', 'info'
 * @param {string} message – the alert text
 */
window.showAlert = function showAlert(type, message) {
    const wrapper = document.createElement('div');
    wrapper.innerHTML = `
    <div class="alert alert-${type} alert-dismissible fade show" role="alert">
      ${message}
      <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
    </div>`;
    document.getElementById('alertPlaceholder').append(wrapper);

    // Optional: auto-dismiss after 5 seconds
    setTimeout(() => {
        const alert = bootstrap.Alert.getOrCreateInstance(wrapper.querySelector('.alert'));
        alert.close();
    }, 5000);
};

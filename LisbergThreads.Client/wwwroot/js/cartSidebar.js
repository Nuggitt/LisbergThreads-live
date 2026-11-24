window.cartSidebar = {
    show: function () {
        const element = document.getElementById('cartSidebar');
        const offcanvas = bootstrap.Offcanvas.getOrCreateInstance(element);
        offcanvas.show();
    },

    hide: function () {
        const element = document.getElementById('cartSidebar');
        const offcanvas = bootstrap.Offcanvas.getOrCreateInstance(element);
        offcanvas.hide();
    },

    goToProduct: function (productId) {
        const element = document.getElementById('cartSidebar');
        const offcanvas = bootstrap.Offcanvas.getOrCreateInstance(element);
        offcanvas.hide();

        setTimeout(function () {
            window.location.href = "/products/" + productId;
        }, 150);
    }
};
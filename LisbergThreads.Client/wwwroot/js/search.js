window.searchOutsideClick = {
    register: function (element, dotNetHelper) {
        document.addEventListener("click", function (event) {
            if (!element.contains(event.target)) {
                dotNetHelper.invokeMethodAsync("CloseSearchFromJs");
            }
        });
    }
};
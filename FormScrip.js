var GS = window.GS || {};

(function () {
    "use strict";

    this.formOnLoad = function (executionContext) {
        var formContext = executionContext.getFormContext();
        
        calculateAge(formContext);
        
        var birthDateAttribute = formContext.getAttribute("gs_birthdate");
        if (birthDateAttribute) {
            birthDateAttribute.addOnChange(function () {
                calculateAge(formContext);
            });
        }
    };

    function calculateAge(formContext) {
        try {
            var birthDateAttribute = formContext.getAttribute("gs_birthdate");
            var ageAttribute = formContext.getAttribute("gs_age");

            if (!birthDateAttribute || !ageAttribute) {
                console.warn("GS.calculateAge: Required fields not found on form");
                return;
            }

            var birthDate = birthDateAttribute.getValue();

            if (!birthDate) {
                ageAttribute.setValue(null);
                return;
            }

            var today = new Date();
            var age = today.getFullYear() - birthDate.getFullYear();
            
            var monthDifference = today.getMonth() - birthDate.getMonth();
            var dayDifference = today.getDate() - birthDate.getDate();
            
            if (monthDifference < 0 || (monthDifference === 0 && dayDifference < 0)) {
                age--;
            }

            if (age < 0 || age > 150) {
                console.warn("GS.calculateAge: Invalid age calculated: " + age);
                ageAttribute.setValue(null);
                return;
            }

            ageAttribute.setValue(age);

        } catch (error) {
            console.error("GS.calculateAge Error: " + error.message);
        }
    }

}).call(GS);
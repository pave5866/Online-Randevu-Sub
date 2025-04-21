document.querySelector('.logo').addEventListener('click', () => {
    document.querySelector('.navbar').classList.toggle('open');
});

const bookedTimes = ["08:00", "10:30", "15:00", "16:30"]; // Example of booked times

const flatpickrOptions = {
    enableTime: true,
    dateFormat: "Y-m-d H:i",
    minDate: "today",
    time_24hr: true,
    minuteIncrement: 30,
    disable: [
        function(date) {
            // Disable Wednesdays and Thursdays
            return (date.getDay() === 3 || date.getDay() === 4);
        }
    ],
    onReady: function(selectedDates, dateStr, instance) {
        instance.calendarContainer.querySelector(".flatpickr-time").style.display = "none"; // Hide the default time picker
        highlightAvailableDays(instance);
        setupTimeSelection(instance, selectedDates);
    },
    onMonthChange: function(selectedDates, dateStr, instance) {
        highlightAvailableDays(instance);
    },
    onValueUpdate: function(selectedDates, dateStr, instance) {
        highlightAvailableDays(instance);
    }
};

function highlightAvailableDays(instance) {
    instance.calendarContainer.querySelectorAll(".flatpickr-day").forEach(dayElem => {
        if ((dayElem.dateObj.getDay() !== 3 && dayElem.dateObj.getDay() !== 4)) {
            dayElem.classList.add("available");
            dayElem.classList.remove("disabled");
        } else {
            dayElem.classList.add("disabled");
            dayElem.classList.remove("available");
        }
    });
}

function setupTimeSelection(instance) {
    const timeContainer = document.createElement("div");
    timeContainer.className = "flatpickr-time-selection";
    const timeSlots = ["7:30" ,"08:00", "08:30", "09:00", "09:30", "10:00", "10:30", 
                       "11:00", "11:30", "12:00", "12:30", "13:00", "13:30", 
                       "14:00", "14:30", "15:00", "15:30", "16:00", "16:30", 
                       "17:00", "17:30", "18:00", "18:30", "19:00", "19:30", 
                       "20:00", "20:30", "21:00", "21:30", "22:00", "22:30",
                       "23:00", "23:30"];

    timeSlots.forEach(time => {
        const button = document.createElement("button");
        button.className = "flatpickr-time-slot";
        button.textContent = time;
        button.dataset.time = time;
        if (bookedTimes.includes(time)) {
            button.classList.add("disabled");
        } else {
            button.addEventListener("click", function () {
                document.querySelectorAll(".flatpickr-time-slot").forEach(btn => btn.classList.remove("active"));
                this.classList.add("active");
                instance.setDate(`${instance.selectedDates[0].toISOString().split('T')[0]} ${time}`, true);
            });
        }
        timeContainer.appendChild(button);
    });

    instance.calendarContainer.appendChild(timeContainer);
}

flatpickr("#datetime", flatpickrOptions);

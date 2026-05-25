/**
 * Form Validation & Enhancement - WCAG 2.1 Level AAA
 * Provides real-time validation, error messaging, and accessibility features
 */

document.addEventListener('DOMContentLoaded', function() {
    const form = document.getElementById('contactForm');
    if (!form) return;

    const nameInput = document.getElementById('contactName');
    const emailInput = document.getElementById('contactEmail');
    const subjectInput = document.getElementById('contactSubject');
    const messageInput = document.getElementById('contactMessage');
    const confirmCheckbox = document.getElementById('confirmSend');
    const submitBtn = form.querySelector('button[type="submit"]');
    const messageCounter = document.getElementById('message-length');

    // Real-time character counter
    if (messageInput) {
        messageInput.addEventListener('input', function() {
            messageCounter.textContent = this.value.length;
        });
    }

    // Real-time validation on input/change
    const inputs = [nameInput, emailInput, subjectInput, messageInput];
    inputs.forEach(input => {
        if (input) {
            input.addEventListener('blur', function() {
                validateField(this);
            });
            input.addEventListener('input', function() {
                // Clear error if user starts correcting
                if (this.classList.contains('is-invalid')) {
                    validateField(this);
                }
            });
        }
    });

    if (confirmCheckbox) {
        confirmCheckbox.addEventListener('change', function() {
            validateConfirmation();
        });
    }

    // Form submission
    form.addEventListener('submit', function(e) {
        e.preventDefault();

        if (validateForm()) {
            // Show confirmation dialog before sending
            if (confirm('Please review your message one more time.\n\nAre you sure you want to send this message?')) {
                // In production, this would submit to the server
                showSuccessMessage();
                form.reset();
                messageCounter.textContent = '0';
                confirmCheckbox.checked = false;
            }
        } else {
            // Focus on first invalid field
            const firstInvalid = form.querySelector('.is-invalid');
            if (firstInvalid) {
                firstInvalid.focus();
                firstInvalid.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        }
    });

    function validateForm() {
        const nameValid = validateField(nameInput);
        const emailValid = validateField(emailInput);
        const subjectValid = validateField(subjectInput);
        const messageValid = validateField(messageInput);
        const confirmValid = validateConfirmation();

        return nameValid && emailValid && subjectValid && messageValid && confirmValid;
    }

    function validateField(field) {
        if (!field) return true;

        let isValid = true;
        let errorMessage = '';

        if (field === nameInput) {
            if (!field.value.trim()) {
                isValid = false;
                errorMessage = 'Name is required';
            } else if (field.value.length < 2) {
                isValid = false;
                errorMessage = 'Name must be at least 2 characters';
            } else if (field.value.length > 100) {
                isValid = false;
                errorMessage = 'Name must be 100 characters or less';
            }
        }

        if (field === emailInput) {
            if (!field.value.trim()) {
                isValid = false;
                errorMessage = 'Email is required';
            } else if (!isValidEmail(field.value)) {
                isValid = false;
                errorMessage = 'Email format is invalid (example: user@domain.com)';
            }
        }

        if (field === subjectInput) {
            if (!field.value.trim()) {
                isValid = false;
                errorMessage = 'Subject is required';
            } else if (field.value.length < 3) {
                isValid = false;
                errorMessage = 'Subject must be at least 3 characters';
            } else if (field.value.length > 100) {
                isValid = false;
                errorMessage = 'Subject must be 100 characters or less';
            }
        }

        if (field === messageInput) {
            if (!field.value.trim()) {
                isValid = false;
                errorMessage = 'Message is required';
            } else if (field.value.length < 10) {
                isValid = false;
                errorMessage = 'Message must be at least 10 characters';
            } else if (field.value.length > 2000) {
                isValid = false;
                errorMessage = 'Message must be 2000 characters or less';
            }
        }

        // Update visual state
        if (isValid) {
            field.classList.remove('is-invalid');
            field.classList.add('is-valid');
        } else {
            field.classList.remove('is-valid');
            field.classList.add('is-invalid');
            // Announce error to screen readers
            const errorElement = document.getElementById(field.id + '-error');
            if (errorElement) {
                errorElement.textContent = errorMessage;
                errorElement.classList.remove('d-none');
                field.setAttribute('aria-invalid', 'true');
            }
        }

        return isValid;
    }

    function validateConfirmation() {
        if (!confirmCheckbox) return true;

        const isValid = confirmCheckbox.checked;

        if (!isValid) {
            confirmCheckbox.classList.add('is-invalid');
        } else {
            confirmCheckbox.classList.remove('is-invalid');
        }

        return isValid;
    }

    function isValidEmail(email) {
        // Simple email validation
        const parts = email.trim().split('@');
        if (parts.length !== 2) return false;

        const local = parts[0];
        const domain = parts[1];

        if (!local || !domain) return false;
        if (domain.indexOf('.') === -1) return false;

        return true;
    }

    function showSuccessMessage() {
        // Create success message
        const successDiv = document.createElement('div');
        successDiv.className = 'alert alert-success alert-dismissible fade show mt-3';
        successDiv.role = 'status';
        successDiv.innerHTML = `
            <i class="bi bi-check-circle me-2" aria-hidden="true"></i>
            <strong>Success!</strong> Your message has been sent. We'll respond within 24-48 hours.
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        `;

        form.parentElement.insertBefore(successDiv, form);
        successDiv.scrollIntoView({ behavior: 'smooth', block: 'nearest' });

        // Auto-dismiss after 5 seconds
        setTimeout(() => {
            const btn = successDiv.querySelector('.btn-close');
            if (btn) btn.click();
        }, 5000);
    }

    // Initialize character counter on page load
    if (messageInput) {
        messageCounter.textContent = messageInput.value.length;
    }
});

// CSS for form validation (can be added to main stylesheet)
const style = document.createElement('style');
style.textContent = `
    .input-kawaii.is-invalid {
        border-color: #dc3545;
        padding-right: calc(1.5em + 0.75rem);
        background-image: url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='12 12 20 20'%3e%3ccircle cx='22' cy='22' r='10' fill='none' stroke='%23dc3545' stroke-width='2'/%3e%3cpath fill='%23dc3545' d='M22 7v10M22 25v2'/%3e%3c/svg%3e");
        background-repeat: no-repeat;
        background-position: right calc(0.375em + 0.1875rem) center;
        background-size: calc(1.5em + 0.75rem) calc(1.5em + 0.75rem);
    }

    .input-kawaii.is-valid {
        border-color: #28a745;
        padding-right: calc(1.5em + 0.75rem);
        background-image: url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='%2328a745'%3e%3cpath d='M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41L9 16.17z'/%3e%3c/svg%3e");
        background-repeat: no-repeat;
        background-position: right calc(0.375em + 0.1875rem) center;
        background-size: calc(1.5em + 0.75rem) calc(1.5em + 0.75rem);
    }

    .invalid-feedback {
        display: block;
        color: #dc3545;
        font-size: 0.875rem;
        margin-top: 0.25rem;
    }

    .form-check-input.is-invalid {
        border-color: #dc3545;
    }

    .form-check-input.is-invalid:checked {
        background-color: #dc3545;
        border-color: #dc3545;
    }
`;
document.head.appendChild(style);

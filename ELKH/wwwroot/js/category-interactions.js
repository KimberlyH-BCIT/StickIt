/**
 * Category Page Interactions
 * Handles category card click interactions for better mobile UX
 */

document.addEventListener('DOMContentLoaded', function() {
    // Add click handler for category cards (for better UX on mobile)
    document.querySelectorAll('.category-card').forEach(card => {
        card.addEventListener('click', function(e) {
            // Only trigger if clicking on the card itself, not on buttons or links
            if (e.target === this || e.target.closest('.card-body') === this.querySelector('.card-body')) {
                const link = this.querySelector('.stretched-link');
                if (link && !link.disabled) {
                    window.location.href = link.href;
                }
            }
        });
    });
});
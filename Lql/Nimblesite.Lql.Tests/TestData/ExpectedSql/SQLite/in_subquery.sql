SELECT users.id, users.name
FROM users
WHERE users.id IN (
    SELECT orders.user_id
    FROM orders
    WHERE orders.status = 'completed'
)

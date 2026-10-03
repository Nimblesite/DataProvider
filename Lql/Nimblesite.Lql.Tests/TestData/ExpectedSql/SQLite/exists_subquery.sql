SELECT users.id, users.name
FROM users
WHERE EXISTS (
    SELECT 1
    FROM orders
    WHERE orders.user_id = users.id AND orders.status = 'completed'
)

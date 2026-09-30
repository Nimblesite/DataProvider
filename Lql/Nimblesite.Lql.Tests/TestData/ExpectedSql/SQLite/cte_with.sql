WITH high_value_customers AS (
    SELECT user_id
    FROM orders
    GROUP BY user_id
    HAVING SUM(total) > 10000
)
SELECT u.id, u.name, u.email
FROM users u
INNER JOIN high_value_customers hvc ON u.id = hvc.user_id

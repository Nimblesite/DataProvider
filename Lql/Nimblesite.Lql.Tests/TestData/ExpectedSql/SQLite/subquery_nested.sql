SELECT u.name, o.order_count
FROM users u
INNER JOIN (
    SELECT user_id, COUNT(*) AS order_count
    FROM orders
    WHERE status = 'completed'
    GROUP BY user_id
) o ON u.id = o.user_id

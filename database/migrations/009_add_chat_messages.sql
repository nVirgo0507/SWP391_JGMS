CREATE TABLE chat_message (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL,
    message TEXT NOT NULL,
    reply TEXT NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_chat_message_user FOREIGN KEY (user_id) REFERENCES "USER" (user_id) ON DELETE CASCADE
);

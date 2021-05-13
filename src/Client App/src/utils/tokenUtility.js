export const tokenConfig = () => {
    // Get token from state
    const token = localStorage.getItem("token");
    // Headers
    const config = {
        headers: {
            "Content-Type": "application/json",
        },
    };
    // If token, add to headers config
    if (token) {
        config.headers["Authorization"] = `Bearer ${token}`;
    }
    return config;
};
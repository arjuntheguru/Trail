import React from 'react';
import { Link } from 'react-router-dom';

const NotFound = () => {
    return (
        <div className="text-center">
            <span className="display-1">404</span>
            <div className="mb-4 lead">The page you are looking for was not found.</div>
            <Link to="/" className="btn btn-link">Back to Home</Link>
        </div>
    )
}

export default NotFound;
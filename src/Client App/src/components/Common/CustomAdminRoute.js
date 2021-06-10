import React from 'react';
import { Route, Redirect } from 'react-router-dom';
import { useDispatch } from 'react-redux';
import { returnLoggedInUser } from '../../store/auth-actions';


const CustomAdminRoute = ({ component: Component, ...rest }) => {

    const dispatch = useDispatch()

    const checkAuth = () => {
        const user = dispatch(returnLoggedInUser())
        if (user) {
            if (user.role === "Admin" || user.role === "Manager") {
                return true
            }
            return false;
        } else {
            return false;
        }
    };


    return (
        <Route exact
            {...rest}
            render={props =>
                checkAuth() === true ? (
                    <Component {...props} />
                ) : (
                    <Redirect to="/NotFound" />
                )
            }
        />
    )
}

export default CustomAdminRoute;
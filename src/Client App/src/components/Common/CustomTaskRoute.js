import React from 'react';
import { Route, Redirect } from 'react-router-dom';
import { useDispatch } from 'react-redux';
import { returnLoggedInUser } from '../../store/auth-actions';
import _ from 'lodash';

const CustomTaskRoute = ({ component: Component, ...rest }) => {

    const dispatch = useDispatch()

    const checkAuth = () => {
        const user = dispatch(returnLoggedInUser())

        var siteId = rest.computedMatch.params.id;
       
        if (user) {
            if (user.role !== "Admin") {
                
                return _.includes(user.siteId, siteId);                
                
            }
            return true;
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

export default CustomTaskRoute;